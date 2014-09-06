using Apache.NMS.Util;
using Lucene.Net.Search;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Lucene.Net.Index
{
    using Lucene.Net.Support;
    using NUnit.Framework;
    using BytesRef = Lucene.Net.Util.BytesRef;

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

    using DeleteSlice = Lucene.Net.Index.DocumentsWriterDeleteQueue.DeleteSlice;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using TermQuery = Lucene.Net.Search.TermQuery;
    using ThreadInterruptedException = Lucene.Net.Util.ThreadInterruptedException;

    /// <summary>
    /// Unit test for <seealso cref="DocumentsWriterDeleteQueue"/>
    /// </summary>
    [TestFixture]
    public class TestDocumentsWriterDeleteQueue : LuceneTestCase
    {
        [Test]
        public virtual void TestUpdateDelteSlices()
        {
            DocumentsWriterDeleteQueue queue = new DocumentsWriterDeleteQueue();
            int size = 200 + Random().Next(500) * RANDOM_MULTIPLIER;
            int?[] ids = new int?[size];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = Random().Next();
            }
            DeleteSlice slice1 = queue.NewSlice();
            DeleteSlice slice2 = queue.NewSlice();
            BufferedUpdates bd1 = new BufferedUpdates();
            BufferedUpdates bd2 = new BufferedUpdates();
            int last1 = 0;
            int last2 = 0;
            HashSet<Term> uniqueValues = new HashSet<Term>();
            for (int j = 0; j < ids.Length; j++)
            {
                int? i = ids[j];
                // create an array here since we compare identity below against tailItem
                Term[] term = new Term[] { new Term("id", i.ToString()) };
                uniqueValues.Add(term[0]);
                queue.AddDelete(term);
                if (Random().Next(20) == 0 || j == ids.Length - 1)
                {
                    queue.UpdateSlice(slice1);
                    Assert.IsTrue(slice1.IsTailItem(term));
                    slice1.Apply(bd1, j);
                    AssertAllBetween(last1, j, bd1, ids);
                    last1 = j + 1;
                }
                if (Random().Next(10) == 5 || j == ids.Length - 1)
                {
                    queue.UpdateSlice(slice2);
                    Assert.IsTrue(slice2.IsTailItem(term));
                    slice2.Apply(bd2, j);
                    AssertAllBetween(last2, j, bd2, ids);
                    last2 = j + 1;
                }
                Assert.AreEqual(j + 1, queue.NumGlobalTermDeletes());
            }
            Assert.AreEqual(uniqueValues, bd1.Terms_Nunit().Keys);
            Assert.AreEqual(uniqueValues, bd2.Terms_Nunit().Keys);
            HashSet<Term> frozenSet = new HashSet<Term>();
            foreach (Term t in queue.FreezeGlobalBuffer(null).TermsIterable())
            {
                BytesRef bytesRef = new BytesRef();
                bytesRef.CopyBytes(t.Bytes());
                frozenSet.Add(new Term(t.Field(), bytesRef));
            }
            Assert.AreEqual(uniqueValues, frozenSet);
            Assert.AreEqual(0, queue.NumGlobalTermDeletes(), "num deletes must be 0 after freeze");
        }

        private void AssertAllBetween(int start, int end, BufferedUpdates deletes, int?[] ids)
        {
            for (int i = start; i <= end; i++)
            {
                Assert.AreEqual(Convert.ToInt32(end), deletes.Terms_Nunit()[new Term("id", ids[i].ToString())]);
            }
        }

        [Test]
        public virtual void TestClear()
        {
            DocumentsWriterDeleteQueue queue = new DocumentsWriterDeleteQueue();
            Assert.IsFalse(queue.AnyChanges());
            queue.Clear();
            Assert.IsFalse(queue.AnyChanges());
            int size = 200 + Random().Next(500) * RANDOM_MULTIPLIER;
            int termsSinceFreeze = 0;
            int queriesSinceFreeze = 0;
            for (int i = 0; i < size; i++)
            {
                Term term = new Term("id", "" + i);
                if (Random().Next(10) == 0)
                {
                    queue.AddDelete(new TermQuery(term));
                    queriesSinceFreeze++;
                }
                else
                {
                    queue.AddDelete(term);
                    termsSinceFreeze++;
                }
                Assert.IsTrue(queue.AnyChanges());
                if (Random().Next(10) == 0)
                {
                    queue.Clear();
                    queue.TryApplyGlobalSlice();
                    Assert.IsFalse(queue.AnyChanges());
                }
            }
        }

        [Test]
        public virtual void TestAnyChanges()
        {
            DocumentsWriterDeleteQueue queue = new DocumentsWriterDeleteQueue();
            int size = 200 + Random().Next(500) * RANDOM_MULTIPLIER;
            int termsSinceFreeze = 0;
            int queriesSinceFreeze = 0;
            for (int i = 0; i < size; i++)
            {
                Term term = new Term("id", "" + i);
                if (Random().Next(10) == 0)
                {
                    queue.AddDelete(new TermQuery(term));
                    queriesSinceFreeze++;
                }
                else
                {
                    queue.AddDelete(term);
                    termsSinceFreeze++;
                }
                Assert.IsTrue(queue.AnyChanges());
                if (Random().Next(5) == 0)
                {
                    FrozenBufferedUpdates freezeGlobalBuffer = queue.FreezeGlobalBuffer(null);
                    Assert.AreEqual(termsSinceFreeze, freezeGlobalBuffer.TermCount);
                    Assert.AreEqual(queriesSinceFreeze, ((Query[])freezeGlobalBuffer.Queries_Nunit()).Length);
                    queriesSinceFreeze = 0;
                    termsSinceFreeze = 0;
                    Assert.IsFalse(queue.AnyChanges());
                }
            }
        }

        //LUCENE TODO: Compilation problems
        /*[Test]
        public virtual void TestPartiallyAppliedGlobalSlice()
        {
            DocumentsWriterDeleteQueue queue = new DocumentsWriterDeleteQueue();
            System.Reflection.FieldInfo field = typeof(DocumentsWriterDeleteQueue).GetField("GlobalBufferLock");
            field.Accessible = true;
            ReentrantLock @lock = (ReentrantLock)field.Get(queue);
            @lock.@Lock();
            ThreadClass t = new ThreadAnonymousInnerClassHelper(this, queue);
            t.Start();
            t.Join();
            @lock.Unlock();
            Assert.IsTrue(queue.AnyChanges(), "changes in del queue but not in slice yet");
            queue.TryApplyGlobalSlice();
            Assert.IsTrue(queue.AnyChanges(), "changes in global buffer");
            FrozenBufferedUpdates freezeGlobalBuffer = queue.FreezeGlobalBuffer(null);
            Assert.IsTrue(freezeGlobalBuffer.Any());
            Assert.AreEqual(1, freezeGlobalBuffer.TermCount);
            Assert.IsFalse(queue.AnyChanges(), "all changes applied");
        }*/

        private class ThreadAnonymousInnerClassHelper : ThreadClass
        {
            private readonly TestDocumentsWriterDeleteQueue OuterInstance;

            private DocumentsWriterDeleteQueue Queue;

            public ThreadAnonymousInnerClassHelper(TestDocumentsWriterDeleteQueue outerInstance, DocumentsWriterDeleteQueue queue)
            {
                this.OuterInstance = outerInstance;
                this.Queue = queue;
            }

            public override void Run()
            {
                Queue.AddDelete(new Term("foo", "bar"));
            }
        }

        [Test]
        public virtual void TestStressDeleteQueue()
        {
            DocumentsWriterDeleteQueue queue = new DocumentsWriterDeleteQueue();
            HashSet<Term> uniqueValues = new HashSet<Term>();
            int size = 10000 + Random().Next(500) * RANDOM_MULTIPLIER;
            int?[] ids = new int?[size];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = Random().Next();
                uniqueValues.Add(new Term("id", ids[i].ToString()));
            }
            CountDownLatch latch = new CountDownLatch(1);
            AtomicInteger index = new AtomicInteger(0);
            int numThreads = 2 + Random().Next(5);
            UpdateThread[] threads = new UpdateThread[numThreads];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new UpdateThread(queue, index, ids, latch);
                threads[i].Start();
            }
            latch.countDown();
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            foreach (UpdateThread updateThread in threads)
            {
                DeleteSlice slice = updateThread.Slice;
                queue.UpdateSlice(slice);
                BufferedUpdates deletes = updateThread.Deletes;
                slice.Apply(deletes, BufferedUpdates.MAX_INT);
                Assert.AreEqual(uniqueValues, deletes.Terms_Nunit().Keys);
            }
            queue.TryApplyGlobalSlice();
            HashSet<Term> frozenSet = new HashSet<Term>();
            foreach (Term t in queue.FreezeGlobalBuffer(null).TermsIterable())
            {
                BytesRef bytesRef = new BytesRef();
                bytesRef.CopyBytes(t.Bytes());
                frozenSet.Add(new Term(t.Field(), bytesRef));
            }
            Assert.AreEqual(0, queue.NumGlobalTermDeletes(), "num deletes must be 0 after freeze");
            Assert.AreEqual(uniqueValues.Count, frozenSet.Count);
            Assert.AreEqual(uniqueValues, frozenSet);
        }

        private class UpdateThread : ThreadClass
        {
            internal readonly DocumentsWriterDeleteQueue Queue;
            internal readonly AtomicInteger Index;
            internal readonly int?[] Ids;
            internal readonly DeleteSlice Slice;
            internal readonly BufferedUpdates Deletes;
            internal readonly CountDownLatch Latch;

            protected internal UpdateThread(DocumentsWriterDeleteQueue queue, AtomicInteger index, int?[] ids, CountDownLatch latch)
            {
                this.Queue = queue;
                this.Index = index;
                this.Ids = ids;
                this.Slice = queue.NewSlice();
                Deletes = new BufferedUpdates();
                this.Latch = latch;
            }

            public override void Run()
            {
                try
                {
                    Latch.@await();
                }
                catch (ThreadInterruptedException e)
                {
                    throw new ThreadInterruptedException(e);
                }
                int i = 0;
                while ((i = Index.GetAndIncrement()) < Ids.Length)
                {
                    Term term = new Term("id", Ids[i].ToString());
                    Queue.Add(term, Slice);
                    Assert.IsTrue(Slice.IsTailItem(term));
                    Slice.Apply(Deletes, BufferedUpdates.MAX_INT);
                }
            }
        }
    }
}