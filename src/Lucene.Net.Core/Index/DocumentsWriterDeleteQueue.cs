using Lucene.Net.Support;
using System;
using System.Threading;

namespace Lucene.Net.Index
{
    using System.Diagnostics;

    /*
         * Licensed to the Apache Software Foundation (ASF) under one or more
         * contributor license agreements. See the NOTICE file distributed with this
         * work for additional information regarding copyright ownership. The ASF
         * licenses this file to You under the Apache License, Version 2.0 (the
         * "License"); you may not use this file except in compliance with the License.
         * You may obtain a copy of the License at
         *
         * http://www.apache.org/licenses/LICENSE-2.0
         *
         * Unless required by applicable law or agreed to in writing, software
         * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
         * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
         * License for the specific language governing permissions and limitations under
         * the License.
         */

    using BinaryDocValuesUpdate = Lucene.Net.Index.DocValuesUpdate.BinaryDocValuesUpdate;
    using NumericDocValuesUpdate = Lucene.Net.Index.DocValuesUpdate.NumericDocValuesUpdate;
    using Query = Lucene.Net.Search.Query;

    /// <summary>
    /// <seealso cref="DocumentsWriterDeleteQueue"/> is a non-blocking linked pending deletes
    /// queue. In contrast to other queue implementation we only maintain the
    /// tail of the queue. A delete queue is always used in a context of a set of
    /// DWPTs and a global delete pool. Each of the DWPT and the global pool need to
    /// maintain their 'own' head of the queue (as a DeleteSlice instance per DWPT).
    /// The difference between the DWPT and the global pool is that the DWPT starts
    /// maintaining a head once it has added its first document since for its segments
    /// private deletes only the deletes after that document are relevant. The global
    /// pool instead starts maintaining the head once this instance is created by
    /// taking the sentinel instance as its initial head.
    /// <p>
    /// Since each <seealso cref="DeleteSlice"/> maintains its own head and the list is only
    /// single linked the garbage collector takes care of pruning the list for us.
    /// All nodes in the list that are still relevant should be either directly or
    /// indirectly referenced by one of the DWPT's private <seealso cref="DeleteSlice"/> or by
    /// the global <seealso cref="BufferedUpdates"/> slice.
    /// <p>
    /// Each DWPT as well as the global delete pool maintain their private
    /// DeleteSlice instance. In the DWPT case updating a slice is equivalent to
    /// atomically finishing the document. The slice update guarantees a "happens
    /// before" relationship to all other updates in the same indexing session. When a
    /// DWPT updates a document it:
    ///
    /// <ol>
    /// <li>consumes a document and finishes its processing</li>
    /// <li>updates its private <seealso cref="DeleteSlice"/> either by calling
    /// <seealso cref="#updateSlice(DeleteSlice)"/> or <seealso cref="#add(Term, DeleteSlice)"/> (if the
    /// document has a delTerm)</li>
    /// <li>applies all deletes in the slice to its private <seealso cref="BufferedUpdates"/>
    /// and resets it</li>
    /// <li>increments its internal document id</li>
    /// </ol>
    ///
    /// The DWPT also doesn't apply its current documents delete term until it has
    /// updated its delete slice which ensures the consistency of the update. If the
    /// update fails before the DeleteSlice could have been updated the deleteTerm
    /// will also not be added to its private deletes neither to the global deletes.
    ///
    /// </summary>
    public sealed class DocumentsWriterDeleteQueue
    {
        private Node Tail; // .NET port: can't use type without specifying type parameter, also not volatile due to Interlocked

        // .NET port: no need for AtomicReferenceFieldUpdater, we can use Interlocked instead
        private readonly DeleteSlice GlobalSlice;

        private readonly BufferedUpdates GlobalBufferedUpdates;
        /* only acquired to update the global deletes */
        private readonly ReentrantLock GlobalBufferLock = new ReentrantLock();

        internal readonly long Generation;

        public DocumentsWriterDeleteQueue()
            : this(0)
        {
        }

        public DocumentsWriterDeleteQueue(long generation)
            : this(new BufferedUpdates(), generation)
        {
        }

        public DocumentsWriterDeleteQueue(BufferedUpdates globalBufferedUpdates, long generation)
        {
            this.GlobalBufferedUpdates = globalBufferedUpdates;
            this.Generation = generation;
            /*
             * we use a sentinel instance as our initial tail. No slice will ever try to
             * apply this tail since the head is always omitted.
             */
            Tail = new Node(null); // sentinel
            GlobalSlice = new DeleteSlice(Tail);
        }

        public void AddDelete(params Query[] queries)
        {
            Add(new QueryArrayNode(queries));
            TryApplyGlobalSlice();
        }

        public void AddDelete(params Term[] terms)
        {
            Add(new TermArrayNode(terms));
            TryApplyGlobalSlice();
        }

        internal void AddNumericUpdate(NumericDocValuesUpdate update)
        {
            Add(new NumericUpdateNode(update));
            TryApplyGlobalSlice();
        }

        internal void AddBinaryUpdate(BinaryDocValuesUpdate update)
        {
            Add(new BinaryUpdateNode(update));
            TryApplyGlobalSlice();
        }

        /// <summary>
        /// invariant for document update
        /// </summary>
        public void Add(Term term, DeleteSlice slice)
        {
            TermNode termNode = new TermNode(term);
            Add(termNode);
            /*
             * this is an update request where the term is the updated documents
             * delTerm. in that case we need to guarantee that this insert is atomic
             * with regards to the given delete slice. this means if two threads try to
             * update the same document with in turn the same delTerm one of them must
             * win. By taking the node we have created for our del term as the new tail
             * it is guaranteed that if another thread adds the same right after us we
             * will apply this delete next time we update our slice and one of the two
             * competing updates wins!
             */
            slice.SliceTail = termNode;
            Debug.Assert(slice.SliceHead != slice.SliceTail, "slice head and tail must differ after add");
            TryApplyGlobalSlice(); // TODO doing this each time is not necessary maybe
            // we can do it just every n times or so?
        }

        private void Add(Node item)
        {
            /*
             * this non-blocking / 'wait-free' linked list add was inspired by Apache
             * Harmony's ConcurrentLinkedQueue Implementation.
             */
            while (true)
            {
                Node currentTail = this.Tail;
                Node tailNext = currentTail.Next;
                if (Tail == currentTail)
                {
                    if (tailNext != null)
                    {
                        /*
                         * we are in intermediate state here. the tails next pointer has been
                         * advanced but the tail itself might not be updated yet. help to
                         * advance the tail and try again updating it.
                         */
                        Interlocked.CompareExchange(ref Tail, tailNext, currentTail); // can fail
                    }
                    else
                    {
                        /*
                         * we are in quiescent state and can try to insert the item to the
                         * current tail if we fail to insert we just retry the operation since
                         * somebody else has already added its item
                         */
                        if (currentTail.CasNext(null, item))
                        {
                            /*
                             * now that we are done we need to advance the tail while another
                             * thread could have advanced it already so we can ignore the return
                             * type of this CAS call
                             */
                            Interlocked.CompareExchange(ref Tail, item, currentTail);
                            return;
                        }
                    }
                }
            }
        }

        public bool AnyChanges()
        {
            GlobalBufferLock.@Lock();
            try
            {
                /*
                 * check if all items in the global slice were applied
                 * and if the global slice is up-to-date
                 * and if globalBufferedUpdates has changes
                 */
                return GlobalBufferedUpdates.Any() || !GlobalSlice.Empty || GlobalSlice.SliceTail != Tail || Tail.Next != null;
            }
            finally
            {
                GlobalBufferLock.Unlock();
            }
        }

        public void TryApplyGlobalSlice()
        {
            if (GlobalBufferLock.TryLock())
            {
                /*
                 * The global buffer must be locked but we don't need to update them if
                 * there is an update going on right now. It is sufficient to apply the
                 * deletes that have been added after the current in-flight global slices
                 * tail the next time we can get the lock!
                 */
                try
                {
                    if (UpdateSlice(GlobalSlice))
                    {
                        //          System.out.println(Thread.currentThread() + ": apply globalSlice");
                        GlobalSlice.Apply(GlobalBufferedUpdates, BufferedUpdates.MAX_INT);
                    }
                }
                finally
                {
                    GlobalBufferLock.Unlock();
                }
            }
        }

        public FrozenBufferedUpdates FreezeGlobalBuffer(DeleteSlice callerSlice)
        {
            GlobalBufferLock.@Lock();
            /*
             * Here we freeze the global buffer so we need to lock it, apply all
             * deletes in the queue and reset the global slice to let the GC prune the
             * queue.
             */
            Node currentTail = Tail; // take the current tail make this local any
            // Changes after this call are applied later
            // and not relevant here
            if (callerSlice != null)
            {
                // Update the callers slices so we are on the same page
                callerSlice.SliceTail = currentTail;
            }
            try
            {
                if (GlobalSlice.SliceTail != currentTail)
                {
                    GlobalSlice.SliceTail = currentTail;
                    GlobalSlice.Apply(GlobalBufferedUpdates, BufferedUpdates.MAX_INT);
                }

                FrozenBufferedUpdates packet = new FrozenBufferedUpdates(GlobalBufferedUpdates, false);
                GlobalBufferedUpdates.Clear();
                return packet;
            }
            finally
            {
                GlobalBufferLock.Unlock();
            }
        }

        public DeleteSlice NewSlice()
        {
            return new DeleteSlice(Tail);
        }

        public bool UpdateSlice(DeleteSlice slice)
        {
            if (slice.SliceTail != Tail) // If we are the same just
            {
                slice.SliceTail = Tail;
                return true;
            }
            return false;
        }

        public class DeleteSlice
        {
            // No need to be volatile, slices are thread captive (only accessed by one thread)!
            internal Node SliceHead; // we don't apply this one

            internal Node SliceTail;

            internal DeleteSlice(Node currentTail)
            {
                Debug.Assert(currentTail != null);
                /*
                 * Initially this is a 0 length slice pointing to the 'current' tail of
                 * the queue. Once we update the slice we only need to assign the tail and
                 * have a new slice
                 */
                SliceHead = SliceTail = currentTail;
            }

            public virtual void Apply(BufferedUpdates del, int docIDUpto)
            {
                if (SliceHead == SliceTail)
                {
                    // 0 length slice
                    return;
                }
                /*
                 * When we apply a slice we take the head and get its next as our first
                 * item to apply and continue until we applied the tail. If the head and
                 * tail in this slice are not equal then there will be at least one more
                 * non-null node in the slice!
                 */
                Node current = SliceHead;
                do
                {
                    current = current.Next;
                    Debug.Assert(current != null, "slice property violated between the head on the tail must not be a null node");
                    current.Apply(del, docIDUpto);
                    //        System.out.println(Thread.currentThread().getName() + ": pull " + current + " docIDUpto=" + docIDUpto);
                } while (current != SliceTail);
                Reset();
            }

            internal virtual void Reset()
            {
                // Reset to a 0 length slice
                SliceHead = SliceTail;
            }

            /// <summary>
            /// Returns <code>true</code> iff the given item is identical to the item
            /// hold by the slices tail, otherwise <code>false</code>.
            /// </summary>
            public virtual bool IsTailItem(object item)
            {
                return SliceTail.Item == item;
            }

            internal virtual bool Empty
            {
                get
                {
                    return SliceHead == SliceTail;
                }
            }
        }

        public int NumGlobalTermDeletes()
        {
            return GlobalBufferedUpdates.NumTermDeletes.Get();
        }

        public void Clear()
        {
            GlobalBufferLock.@Lock();
            try
            {
                Node currentTail = Tail;
                GlobalSlice.SliceHead = GlobalSlice.SliceTail = currentTail;
                GlobalBufferedUpdates.Clear();
            }
            finally
            {
                GlobalBufferLock.Unlock();
            }
        }

        public class Node
        {
            internal /*volatile*/ Node Next;
            internal readonly object Item;

            internal Node(object item)
            {
                this.Item = item;
            }

            //internal static readonly AtomicReferenceFieldUpdater<Node, Node> NextUpdater = AtomicReferenceFieldUpdater.newUpdater(typeof(Node), typeof(Node), "next");

            internal virtual void Apply(BufferedUpdates bufferedDeletes, int docIDUpto)
            {
                throw new InvalidOperationException("sentinel item must never be applied");
            }

            internal virtual bool CasNext(Node cmp, Node val)
            {
                // .NET port: Interlocked.CompareExchange(location, value, comparand) is backwards from
                // AtomicReferenceFieldUpdater.compareAndSet(obj, expect, update), so swapping val and cmp.
                // Also, it doesn't return bool if it was updated, so we need to compare to see if
                // original == comparand to determine whether to return true or false here.
                Node original = Next;
                return ReferenceEquals(Interlocked.CompareExchange(ref Next, val, cmp), original);
            }
        }

        private sealed class TermNode : Node
        {
            internal TermNode(Term term)
                : base(term)
            {
            }

            internal override void Apply(BufferedUpdates bufferedDeletes, int docIDUpto)
            {
                bufferedDeletes.AddTerm((Term)Item, docIDUpto);
            }

            public override string ToString()
            {
                return "del=" + Item;
            }
        }

        private sealed class QueryArrayNode : Node
        {
            internal QueryArrayNode(Query[] query)
                : base(query)
            {
            }

            internal override void Apply(BufferedUpdates bufferedUpdates, int docIDUpto)
            {
                foreach (Query query in (Query[])Item)
                {
                    bufferedUpdates.AddQuery(query, docIDUpto);
                }
            }
        }

        private sealed class TermArrayNode : Node
        {
            internal TermArrayNode(Term[] term)
                : base(term)
            {
            }

            internal override void Apply(BufferedUpdates bufferedUpdates, int docIDUpto)
            {
                foreach (Term term in (Term[])Item)
                {
                    bufferedUpdates.AddTerm(term, docIDUpto);
                }
            }

            public override string ToString()
            {
                return "dels=" + Arrays.ToString((Term[])Item);
            }
        }

        private sealed class NumericUpdateNode : Node
        {
            internal NumericUpdateNode(NumericDocValuesUpdate update)
                : base(update)
            {
            }

            internal override void Apply(BufferedUpdates bufferedUpdates, int docIDUpto)
            {
                bufferedUpdates.AddNumericUpdate((NumericDocValuesUpdate)Item, docIDUpto);
            }

            public override string ToString()
            {
                return "update=" + Item;
            }
        }

        private sealed class BinaryUpdateNode : Node
        {
            internal BinaryUpdateNode(BinaryDocValuesUpdate update)
                : base(update)
            {
            }

            internal override void Apply(BufferedUpdates bufferedUpdates, int docIDUpto)
            {
                bufferedUpdates.AddBinaryUpdate((BinaryDocValuesUpdate)Item, docIDUpto);
            }

            public override string ToString()
            {
                return "update=" + (BinaryDocValuesUpdate)Item;
            }
        }

        private bool ForceApplyGlobalSlice()
        {
            GlobalBufferLock.@Lock();
            Node currentTail = Tail;
            try
            {
                if (GlobalSlice.SliceTail != currentTail)
                {
                    GlobalSlice.SliceTail = currentTail;
                    GlobalSlice.Apply(GlobalBufferedUpdates, BufferedUpdates.MAX_INT);
                }
                return GlobalBufferedUpdates.Any();
            }
            finally
            {
                GlobalBufferLock.Unlock();
            }
        }

        public int BufferedUpdatesTermsSize
        {
            get
            {
                GlobalBufferLock.@Lock();
                try
                {
                    ForceApplyGlobalSlice();
                    return GlobalBufferedUpdates.Terms.Count;
                }
                finally
                {
                    GlobalBufferLock.Unlock();
                }
            }
        }

        public long BytesUsed()
        {
            return GlobalBufferedUpdates.BytesUsed.Get();
        }

        public override string ToString()
        {
            return "DWDQ: [ generation: " + Generation + " ]";
        }
    }
}