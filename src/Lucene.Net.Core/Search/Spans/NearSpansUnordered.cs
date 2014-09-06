using System;
using System.Collections;
using System.Collections.Generic;

namespace Lucene.Net.Search.Spans
{
    using Lucene.Net.Util;

    /*
         * Licensed to the Apache Software Foundation (ASF) under one or more
         * contributor license agreements.  See the NOTICE file distributed with
         * this work for additional information regarding copyright ownership.
         * The ASF licenses this file to You under the Apache License, Version 2.0
         * (the "License"); you may not use this file except in compliance with
         * the License.  You may obtain a copy of the License at
         *
         *     http://www.apache.org/licenses/LICENSE-2.0
         *
         * Unless required by applicable law or agreed to in writing, software
         * distributed under the License is distributed on an "AS IS" BASIS,
         * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
         * See the License for the specific language governing permissions and
         * limitations under the License.
         */

    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
    using Bits = Lucene.Net.Util.Bits;
    using Term = Lucene.Net.Index.Term;
    using TermContext = Lucene.Net.Index.TermContext;

    /// <summary>
    /// Similar to <seealso cref="NearSpansOrdered"/>, but for the unordered case.
    ///
    /// Expert:
    /// Only public for subclassing.  Most implementations should not need this class
    /// </summary>
    public class NearSpansUnordered : Spans
    {
        private SpanNearQuery Query;

        private IList<SpansCell> Ordered = new List<SpansCell>(); // spans in query order
        private Spans[] subSpans;
        private int Slop; // from query

        private SpansCell First; // linked list of spans
        private SpansCell Last; // sorted by doc only

        private int TotalLength; // sum of current lengths

        private CellQueue Queue; // sorted queue of spans
        private SpansCell Max; // max element in queue

        private bool More = true; // true iff not done
        private bool FirstTime = true; // true before first next()

        private class CellQueue : PriorityQueue<SpansCell>
        {
            private readonly NearSpansUnordered OuterInstance;

            public CellQueue(NearSpansUnordered outerInstance, int size)
                : base(size)
            {
                this.OuterInstance = outerInstance;
            }

            public override bool LessThan(SpansCell spans1, SpansCell spans2)
            {
                if (spans1.Doc() == spans2.Doc())
                {
                    return NearSpansOrdered.DocSpansOrdered(spans1, spans2);
                }
                else
                {
                    return spans1.Doc() < spans2.Doc();
                }
            }
        }

        /// <summary>
        /// Wraps a Spans, and can be used to form a linked list. </summary>
        private class SpansCell : Spans
        {
            private readonly NearSpansUnordered OuterInstance;

            internal Spans Spans;
            internal SpansCell Next_Renamed;
            internal int Length = -1;
            internal int Index;

            public SpansCell(NearSpansUnordered outerInstance, Spans spans, int index)
            {
                this.OuterInstance = outerInstance;
                this.Spans = spans;
                this.Index = index;
            }

            public override bool Next()
            {
                return Adjust(Spans.Next());
            }

            public override bool SkipTo(int target)
            {
                return Adjust(Spans.SkipTo(target));
            }

            internal virtual bool Adjust(bool condition)
            {
                if (Length != -1)
                {
                    OuterInstance.TotalLength -= Length; // subtract old length
                }
                if (condition)
                {
                    Length = End() - Start();
                    OuterInstance.TotalLength += Length; // add new length

                    if (OuterInstance.Max == null || Doc() > OuterInstance.Max.Doc() || (Doc() == OuterInstance.Max.Doc()) && (End() > OuterInstance.Max.End()))
                    {
                        OuterInstance.Max = this;
                    }
                }
                OuterInstance.More = condition;
                return condition;
            }

            public override int Doc()
            {
                return Spans.Doc();
            }

            public override int Start()
            {
                return Spans.Start();
            }

            public override int End()
            // TODO: Remove warning after API has been finalized
            {
                return Spans.End();
            }

            public override ICollection<sbyte[]> Payload
            {
                get
                {
                    return new List<sbyte[]>(Spans.Payload);
                }
            }

            // TODO: Remove warning after API has been finalized
            public override bool PayloadAvailable
            {
                get
                {
                    return Spans.PayloadAvailable;
                }
            }

            public override long Cost()
            {
                return Spans.Cost();
            }

            public override string ToString()
            {
                return Spans.ToString() + "#" + Index;
            }
        }

        public NearSpansUnordered(SpanNearQuery query, AtomicReaderContext context, Bits acceptDocs, IDictionary<Term, TermContext> termContexts)
        {
            this.Query = query;
            this.Slop = query.Slop;

            SpanQuery[] clauses = query.Clauses;
            Queue = new CellQueue(this, clauses.Length);
            subSpans = new Spans[clauses.Length];
            for (int i = 0; i < clauses.Length; i++)
            {
                SpansCell cell = new SpansCell(this, clauses[i].GetSpans(context, acceptDocs, termContexts), i);
                Ordered.Add(cell);
                subSpans[i] = cell.Spans;
            }
        }

        public virtual Spans[] SubSpans
        {
            get
            {
                return subSpans;
            }
        }

        public override bool Next()
        {
            if (FirstTime)
            {
                InitList(true);
                ListToQueue(); // initialize queue
                FirstTime = false;
            }
            else if (More)
            {
                if (Min().Next()) // trigger further scanning
                {
                    Queue.UpdateTop(); // maintain queue
                }
                else
                {
                    More = false;
                }
            }

            while (More)
            {
                bool queueStale = false;

                if (Min().Doc() != Max.Doc()) // maintain list
                {
                    QueueToList();
                    queueStale = true;
                }

                // skip to doc w/ all clauses

                while (More && First.Doc() < Last.Doc())
                {
                    More = First.SkipTo(Last.Doc()); // skip first upto last
                    FirstToLast(); // and move it to the end
                    queueStale = true;
                }

                if (!More)
                {
                    return false;
                }

                // found doc w/ all clauses

                if (queueStale) // maintain the queue
                {
                    ListToQueue();
                    queueStale = false;
                }

                if (AtMatch())
                {
                    return true;
                }

                More = Min().Next();
                if (More)
                {
                    Queue.UpdateTop(); // maintain queue
                }
            }
            return false; // no more matches
        }

        public override bool SkipTo(int target)
        {
            if (FirstTime) // initialize
            {
                InitList(false);
                for (SpansCell cell = First; More && cell != null; cell = cell.Next_Renamed)
                {
                    More = cell.SkipTo(target); // skip all
                }
                if (More)
                {
                    ListToQueue();
                }
                FirstTime = false;
            } // normal case
            else
            {
                while (More && Min().Doc() < target) // skip as needed
                {
                    if (Min().SkipTo(target))
                    {
                        Queue.UpdateTop();
                    }
                    else
                    {
                        More = false;
                    }
                }
            }
            return More && (AtMatch() || Next());
        }

        private SpansCell Min()
        {
            return Queue.Top();
        }

        public override int Doc()
        {
            return Min().Doc();
        }

        public override int Start()
        {
            return Min().Start();
        }

        public override int End()
        // TODO: Remove warning after API has been finalized
        /// <summary>
        /// WARNING: The List is not necessarily in order of the the positions </summary>
        /// <returns> Collection of <code>byte[]</code> payloads </returns>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        {
            return Max.End();
        }

        public override ICollection<sbyte[]> Payload
        {
            get
            {
                HashSet<sbyte[]> matchPayload = new HashSet<sbyte[]>();
                for (SpansCell cell = First; cell != null; cell = cell.Next_Renamed)
                {
                    if (cell.PayloadAvailable)
                    {
                        matchPayload.UnionWith(cell.Payload);
                    }
                }
                return matchPayload;
            }
        }

        // TODO: Remove warning after API has been finalized
        public override bool PayloadAvailable
        {
            get
            {
                SpansCell pointer = Min();
                while (pointer != null)
                {
                    if (pointer.PayloadAvailable)
                    {
                        return true;
                    }
                    pointer = pointer.Next_Renamed;
                }

                return false;
            }
        }

        public override long Cost()
        {
            long minCost = long.MaxValue;
            for (int i = 0; i < subSpans.Length; i++)
            {
                minCost = Math.Min(minCost, subSpans[i].Cost());
            }
            return minCost;
        }

        public override string ToString()
        {
            return this.GetType().Name + "(" + Query.ToString() + ")@" + (FirstTime ? "START" : (More ? (Doc() + ":" + Start() + "-" + End()) : "END"));
        }

        private void InitList(bool next)
        {
            for (int i = 0; More && i < Ordered.Count; i++)
            {
                SpansCell cell = Ordered[i];
                if (next)
                {
                    More = cell.Next(); // move to first entry
                }
                if (More)
                {
                    AddToList(cell); // add to list
                }
            }
        }

        private void AddToList(SpansCell cell)
        {
            if (Last != null) // add next to end of list
            {
                Last.Next_Renamed = cell;
            }
            else
            {
                First = cell;
            }
            Last = cell;
            cell.Next_Renamed = null;
        }

        private void FirstToLast()
        {
            Last.Next_Renamed = First; // move first to end of list
            Last = First;
            First = First.Next_Renamed;
            Last.Next_Renamed = null;
        }

        private void QueueToList()
        {
            Last = First = null;
            while (Queue.Top() != null)
            {
                AddToList(Queue.Pop());
            }
        }

        private void ListToQueue()
        {
            Queue.Clear(); // rebuild queue
            for (SpansCell cell = First; cell != null; cell = cell.Next_Renamed)
            {
                Queue.Add(cell); // add to queue from list
            }
        }

        private bool AtMatch()
        {
            return (Min().Doc() == Max.Doc()) && ((Max.End() - Min().Start() - TotalLength) <= Slop);
        }
    }
}