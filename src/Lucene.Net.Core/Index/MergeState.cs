using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Index
{
    using Bits = Lucene.Net.Util.Bits;

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

    using Directory = Lucene.Net.Store.Directory;
    using InfoStream = Lucene.Net.Util.InfoStream;
    using MonotonicAppendingLongBuffer = Lucene.Net.Util.Packed.MonotonicAppendingLongBuffer;

    /// <summary>
    /// Holds common state used during segment merging.
    ///
    /// @lucene.experimental
    /// </summary>
    public class MergeState
    {
        /// <summary>
        /// Remaps docids around deletes during merge
        /// </summary>
        public abstract class DocMap
        {
            internal DocMap()
            {
            }

            /// <summary>
            /// Returns the mapped docID corresponding to the provided one. </summary>
            public abstract int Get(int docID);

            /// <summary>
            /// Returns the total number of documents, ignoring
            ///  deletions.
            /// </summary>
            public abstract int MaxDoc();

            /// <summary>
            /// Returns the number of not-deleted documents. </summary>
            public int NumDocs()
            {
                return MaxDoc() - NumDeletedDocs();
            }

            /// <summary>
            /// Returns the number of deleted documents. </summary>
            public abstract int NumDeletedDocs();

            /// <summary>
            /// Returns true if there are any deletions. </summary>
            public virtual bool HasDeletions()
            {
                return NumDeletedDocs() > 0;
            }

            /// <summary>
            /// Creates a <seealso cref="DocMap"/> instance appropriate for
            ///  this reader.
            /// </summary>
            public static DocMap Build(AtomicReader reader)
            {
                int maxDoc = reader.MaxDoc();
                if (!reader.HasDeletions())
                {
                    return new NoDelDocMap(maxDoc);
                }
                Bits liveDocs = reader.LiveDocs;
                return Build(maxDoc, liveDocs);
            }

            public static DocMap Build(int maxDoc, Bits liveDocs)
            {
                Debug.Assert(liveDocs != null);
                MonotonicAppendingLongBuffer docMap = new MonotonicAppendingLongBuffer();
                int del = 0;
                for (int i = 0; i < maxDoc; ++i)
                {
                    docMap.Add(i - del);
                    if (!liveDocs.Get(i))
                    {
                        ++del;
                    }
                }
                docMap.Freeze();
                int numDeletedDocs = del;
                Debug.Assert(docMap.Size() == maxDoc);
                return new DocMapAnonymousInnerClassHelper(maxDoc, liveDocs, docMap, numDeletedDocs);
            }

            private class DocMapAnonymousInnerClassHelper : DocMap
            {
                private int maxDoc;
                private Bits LiveDocs;
                private MonotonicAppendingLongBuffer DocMap;
                private int numDeletedDocs;

                public DocMapAnonymousInnerClassHelper(int maxDoc, Bits liveDocs, MonotonicAppendingLongBuffer docMap, int numDeletedDocs)
                {
                    this.maxDoc = maxDoc;
                    this.LiveDocs = liveDocs;
                    this.DocMap = docMap;
                    this.numDeletedDocs = numDeletedDocs;
                }

                public override int Get(int docID)
                {
                    if (!LiveDocs.Get(docID))
                    {
                        return -1;
                    }
                    return (int)DocMap.Get(docID);
                }

                public override int MaxDoc()
                {
                    return maxDoc;
                }

                public override int NumDeletedDocs()
                {
                    return numDeletedDocs;
                }
            }
        }

        private sealed class NoDelDocMap : DocMap
        {
            internal readonly int MaxDoc_Renamed;

            internal NoDelDocMap(int maxDoc)
            {
                this.MaxDoc_Renamed = maxDoc;
            }

            public override int Get(int docID)
            {
                return docID;
            }

            public override int MaxDoc()
            {
                return MaxDoc_Renamed;
            }

            public override int NumDeletedDocs()
            {
                return 0;
            }
        }

        /// <summary>
        /// <seealso cref="SegmentInfo"/> of the newly merged segment. </summary>
        public readonly SegmentInfo SegmentInfo;

        /// <summary>
        /// <seealso cref="FieldInfos"/> of the newly merged segment. </summary>
        public FieldInfos FieldInfos;

        /// <summary>
        /// Readers being merged. </summary>
        public readonly IList<AtomicReader> Readers;

        /// <summary>
        /// Maps docIDs around deletions. </summary>
        public DocMap[] DocMaps;

        /// <summary>
        /// New docID base per reader. </summary>
        public int[] DocBase;

        /// <summary>
        /// Holds the CheckAbort instance, which is invoked
        ///  periodically to see if the merge has been aborted.
        /// </summary>
        public readonly CheckAbort checkAbort;

        /// <summary>
        /// InfoStream for debugging messages. </summary>
        public readonly InfoStream InfoStream;

        // TODO: get rid of this? it tells you which segments are 'aligned' (e.g. for bulk merging)
        // but is this really so expensive to compute again in different components, versus once in SM?

        /// <summary>
        /// <seealso cref="SegmentReader"/>s that have identical field
        /// name/number mapping, so their stored fields and term
        /// vectors may be bulk merged.
        /// </summary>
        public SegmentReader[] MatchingSegmentReaders;

        /// <summary>
        /// How many <seealso cref="#matchingSegmentReaders"/> are set. </summary>
        public int MatchedCount;

        /// <summary>
        /// Sole constructor. </summary>
        internal MergeState(IList<AtomicReader> readers, SegmentInfo segmentInfo, InfoStream infoStream, CheckAbort checkAbort_)
        {
            this.Readers = readers;
            this.SegmentInfo = segmentInfo;
            this.InfoStream = infoStream;
            this.checkAbort = checkAbort_;
        }

        /// <summary>
        /// Class for recording units of work when merging segments.
        /// </summary>
        public class CheckAbort
        {
            internal double WorkCount;
            internal readonly MergePolicy.OneMerge Merge;
            internal readonly Directory Dir;

            /// <summary>
            /// Creates a #CheckAbort instance. </summary>
            public CheckAbort(MergePolicy.OneMerge merge, Directory dir)
            {
                this.Merge = merge;
                this.Dir = dir;
            }

            /// <summary>
            /// Records the fact that roughly units amount of work
            /// have been done since this method was last called.
            /// When adding time-consuming code into SegmentMerger,
            /// you should test different values for units to ensure
            /// that the time in between calls to merge.checkAborted
            /// is up to ~ 1 second.
            /// </summary>
            public virtual void Work(double units)
            {
                WorkCount += units;
                if (WorkCount >= 10000.0)
                {
                    Merge.CheckAborted(Dir);
                    WorkCount = 0;
                }
            }

            /// <summary>
            /// If you use this: IW.close(false) cannot abort your merge!
            /// @lucene.internal
            /// </summary>
            public static readonly MergeState.CheckAbort NONE = new CheckAbortAnonymousInnerClassHelper();

            private class CheckAbortAnonymousInnerClassHelper : MergeState.CheckAbort
            {
                public CheckAbortAnonymousInnerClassHelper()
                    : base(null, null)
                {
                }

                public override void Work(double units)
                {
                    // do nothing
                }
            }
        }
    }
}