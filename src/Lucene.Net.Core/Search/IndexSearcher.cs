using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Search
{
    using Lucene.Net.Index; // javadocs
    using Lucene.Net.Support;
    using System.Threading.Tasks;
    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
    using DefaultSimilarity = Lucene.Net.Search.Similarities.DefaultSimilarity;

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

    using Document = Lucene.Net.Document.Document;

    // javadocs
    using IndexReader = Lucene.Net.Index.IndexReader;
    using IndexReaderContext = Lucene.Net.Index.IndexReaderContext;
    using MultiFields = Lucene.Net.Index.MultiFields;
    using NIOFSDirectory = Lucene.Net.Store.NIOFSDirectory; // javadoc
    using ReaderUtil = Lucene.Net.Index.ReaderUtil;
    using Similarity = Lucene.Net.Search.Similarities.Similarity;
    using StoredFieldVisitor = Lucene.Net.Index.StoredFieldVisitor;
    using Term = Lucene.Net.Index.Term;
    using TermContext = Lucene.Net.Index.TermContext;
    using Terms = Lucene.Net.Index.Terms;

    /// <summary>
    /// Implements search over a single IndexReader.
    ///
    /// <p>Applications usually need only call the inherited
    /// <seealso cref="#search(Query,int)"/>
    /// or <seealso cref="#search(Query,Filter,int)"/> methods. For
    /// performance reasons, if your index is unchanging, you
    /// should share a single IndexSearcher instance across
    /// multiple searches instead of creating a new one
    /// per-search.  If your index has changed and you wish to
    /// see the changes reflected in searching, you should
    /// use <seealso cref="DirectoryReader#openIfChanged(DirectoryReader)"/>
    /// to obtain a new reader and
    /// then create a new IndexSearcher from that.  Also, for
    /// low-latency turnaround it's best to use a near-real-time
    /// reader (<seealso cref="DirectoryReader#open(IndexWriter,boolean)"/>).
    /// Once you have a new <seealso cref="IndexReader"/>, it's relatively
    /// cheap to create a new IndexSearcher from it.
    ///
    /// <a name="thread-safety"></a><p><b>NOTE</b>: <code>{@link
    /// IndexSearcher}</code> instances are completely
    /// thread safe, meaning multiple threads can call any of its
    /// methods, concurrently.  If your application requires
    /// external synchronization, you should <b>not</b>
    /// synchronize on the <code>IndexSearcher</code> instance;
    /// use your own (non-Lucene) objects instead.</p>
    /// </summary>
    public class IndexSearcher
    {
        internal readonly IndexReader Reader; // package private for testing!

        // NOTE: these members might change in incompatible ways
        // in the next release
        protected internal readonly IndexReaderContext ReaderContext;

        protected internal readonly IList<AtomicReaderContext> LeafContexts;

        /// <summary>
        /// used with executor - each slice holds a set of leafs executed within one thread </summary>
        protected internal readonly LeafSlice[] LeafSlices;

        // These are only used for multi-threaded search
        private readonly TaskScheduler Executor;

        // the default Similarity
        private static readonly Similarity DefaultSimilarity_Renamed = new DefaultSimilarity();

        /// <summary>
        /// Expert: returns a default Similarity instance.
        /// In general, this method is only called to initialize searchers and writers.
        /// User code and query implementations should respect
        /// <seealso cref="IndexSearcher#getSimilarity()"/>.
        /// @lucene.internal
        /// </summary>
        public static Similarity DefaultSimilarity
        {
            get
            {
                return DefaultSimilarity_Renamed;
            }
        }

        /// <summary>
        /// The Similarity implementation used by this searcher. </summary>
        private Similarity Similarity_Renamed = DefaultSimilarity_Renamed;

        /// <summary>
        /// Creates a searcher searching the provided index. </summary>
        public IndexSearcher(IndexReader r)
            : this(r, null)
        {
        }

        /// <summary>
        /// Runs searches for each segment separately, using the
        ///  provided ExecutorService.  IndexSearcher will not
        ///  shutdown/awaitTermination this ExecutorService on
        ///  close; you must do so, eventually, on your own.  NOTE:
        ///  if you are using <seealso cref="NIOFSDirectory"/>, do not use
        ///  the shutdownNow method of ExecutorService as this uses
        ///  Thread.interrupt under-the-hood which can silently
        ///  close file descriptors (see <a
        ///  href="https://issues.apache.org/jira/browse/LUCENE-2239">LUCENE-2239</a>).
        ///
        /// @lucene.experimental
        /// </summary>
        public IndexSearcher(IndexReader r, TaskScheduler executor)
            : this(r.Context, executor)
        {
        }

        /// <summary>
        /// Creates a searcher searching the provided top-level <seealso cref="IndexReaderContext"/>.
        /// <p>
        /// Given a non-<code>null</code> <seealso cref="ExecutorService"/> this method runs
        /// searches for each segment separately, using the provided ExecutorService.
        /// IndexSearcher will not shutdown/awaitTermination this ExecutorService on
        /// close; you must do so, eventually, on your own. NOTE: if you are using
        /// <seealso cref="NIOFSDirectory"/>, do not use the shutdownNow method of
        /// ExecutorService as this uses Thread.interrupt under-the-hood which can
        /// silently close file descriptors (see <a
        /// href="https://issues.apache.org/jira/browse/LUCENE-2239">LUCENE-2239</a>).
        /// </summary>
        /// <seealso cref= IndexReaderContext </seealso>
        /// <seealso cref= IndexReader#getContext()
        /// @lucene.experimental </seealso>
        public IndexSearcher(IndexReaderContext context, TaskScheduler executor)
        {
            Debug.Assert(context.IsTopLevel, "IndexSearcher's ReaderContext must be topLevel for reader" + context.Reader());
            Reader = context.Reader();
            this.Executor = executor;
            this.ReaderContext = context;
            LeafContexts = context.Leaves();
            this.LeafSlices = executor == null ? null : Slices(LeafContexts);
        }

        /// <summary>
        /// Creates a searcher searching the provided top-level <seealso cref="IndexReaderContext"/>.
        /// </summary>
        /// <seealso cref= IndexReaderContext </seealso>
        /// <seealso cref= IndexReader#getContext()
        /// @lucene.experimental </seealso>
        public IndexSearcher(IndexReaderContext context)
            : this(context, null)
        {
        }

        /// <summary>
        /// Expert: Creates an array of leaf slices each holding a subset of the given leaves.
        /// Each <seealso cref="LeafSlice"/> is executed in a single thread. By default there
        /// will be one <seealso cref="LeafSlice"/> per leaf (<seealso cref="AtomicReaderContext"/>).
        /// </summary>
        protected internal virtual LeafSlice[] Slices(IList<AtomicReaderContext> leaves)
        {
            LeafSlice[] slices = new LeafSlice[leaves.Count];
            for (int i = 0; i < slices.Length; i++)
            {
                slices[i] = new LeafSlice(leaves[i]);
            }
            return slices;
        }

        /// <summary>
        /// Return the <seealso cref="IndexReader"/> this searches. </summary>
        public virtual IndexReader IndexReader
        {
            get
            {
                return Reader;
            }
        }

        /// <summary>
        /// Sugar for <code>.getIndexReader().document(docID)</code> </summary>
        /// <seealso cref= IndexReader#document(int)  </seealso>
        public virtual Document Doc(int docID)
        {
            return Reader.Document(docID);
        }

        /// <summary>
        /// Sugar for <code>.getIndexReader().document(docID, fieldVisitor)</code> </summary>
        /// <seealso cref= IndexReader#document(int, StoredFieldVisitor)  </seealso>
        public virtual void Doc(int docID, StoredFieldVisitor fieldVisitor)
        {
            Reader.Document(docID, fieldVisitor);
        }

        /// <summary>
        /// Sugar for <code>.getIndexReader().document(docID, fieldsToLoad)</code> </summary>
        /// <seealso cref= IndexReader#document(int, Set)  </seealso>
        public virtual Document Doc(int docID, ISet<string> fieldsToLoad)
        {
            return Reader.Document(docID, fieldsToLoad);
        }

        /// @deprecated Use <seealso cref="#doc(int, Set)"/> instead.
        [Obsolete("Use <seealso cref=#doc(int, java.util.Set)/> instead.")]
        public Document Document(int docID, ISet<string> fieldsToLoad)
        {
            return Doc(docID, fieldsToLoad);
        }

        /// <summary>
        /// Expert: Set the Similarity implementation used by this IndexSearcher.
        ///
        /// </summary>
        public virtual Similarity Similarity
        {
            set
            {
                this.Similarity_Renamed = value;
            }
            get
            {
                return Similarity_Renamed;
            }
        }

        /// <summary>
        /// @lucene.internal </summary>
        protected internal virtual Query WrapFilter(Query query, Filter filter)
        {
            return (filter == null) ? query : new FilteredQuery(query, filter);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code> where all results are after a previous
        /// result (<code>after</code>).
        /// <p>
        /// By passing the bottom result from a previous page as <code>after</code>,
        /// this method can be used for efficient 'deep-paging' across potentially
        /// large result sets.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs SearchAfter(ScoreDoc after, Query query, int n)
        {
            return Search(CreateNormalizedWeight(query), after, n);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code>, applying <code>filter</code> if non-null,
        /// where all results are after a previous result (<code>after</code>).
        /// <p>
        /// By passing the bottom result from a previous page as <code>after</code>,
        /// this method can be used for efficient 'deep-paging' across potentially
        /// large result sets.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs SearchAfter(ScoreDoc after, Query query, Filter filter, int n)
        {
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), after, n);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code>.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs Search(Query query, int n)
        {
            return Search(query, null, n);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code>, applying <code>filter</code> if non-null.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs Search(Query query, Filter filter, int n)
        {
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), null, n);
        }

        /// <summary>
        /// Lower-level search API.
        ///
        /// <p><seealso cref="Collector#collect(int)"/> is called for every matching
        /// document.
        /// </summary>
        /// <param name="query"> to match documents </param>
        /// <param name="filter"> if non-null, used to permit documents to be collected. </param>
        /// <param name="results"> to receive hits </param>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual void Search(Query query, Filter filter, Collector results)
        {
            Search(LeafContexts, CreateNormalizedWeight(WrapFilter(query, filter)), results);
        }

        /// <summary>
        /// Lower-level search API.
        ///
        /// <p><seealso cref="Collector#collect(int)"/> is called for every matching document.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual void Search(Query query, Collector results)
        {
            Search(LeafContexts, CreateNormalizedWeight(query), results);
        }

        /// <summary>
        /// Search implementation with arbitrary sorting.  Finds
        /// the top <code>n</code> hits for <code>query</code>, applying
        /// <code>filter</code> if non-null, and sorting the hits by the criteria in
        /// <code>sort</code>.
        ///
        /// <p>NOTE: this does not compute scores by default; use
        /// <seealso cref="IndexSearcher#search(Query,Filter,int,Sort,boolean,boolean)"/> to
        /// control scoring.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopFieldDocs Search(Query query, Filter filter, int n, Sort sort)
        {
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), n, sort, false, false);
        }

        /// <summary>
        /// Search implementation with arbitrary sorting, plus
        /// control over whether hit scores and max score
        /// should be computed.  Finds
        /// the top <code>n</code> hits for <code>query</code>, applying
        /// <code>filter</code> if non-null, and sorting the hits by the criteria in
        /// <code>sort</code>.  If <code>doDocScores</code> is <code>true</code>
        /// then the score of each hit will be computed and
        /// returned.  If <code>doMaxScore</code> is
        /// <code>true</code> then the maximum score over all
        /// collected hits will be computed.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopFieldDocs Search(Query query, Filter filter, int n, Sort sort, bool doDocScores, bool doMaxScore)
        {
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), n, sort, doDocScores, doMaxScore);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code>, applying <code>filter</code> if non-null,
        /// where all results are after a previous result (<code>after</code>).
        /// <p>
        /// By passing the bottom result from a previous page as <code>after</code>,
        /// this method can be used for efficient 'deep-paging' across potentially
        /// large result sets.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs SearchAfter(ScoreDoc after, Query query, Filter filter, int n, Sort sort)
        {
            if (after != null && !(after is FieldDoc))
            {
                // TODO: if we fix type safety of TopFieldDocs we can
                // remove this
                throw new System.ArgumentException("after must be a FieldDoc; got " + after);
            }
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), (FieldDoc)after, n, sort, true, false, false);
        }

        /// <summary>
        /// Search implementation with arbitrary sorting and no filter. </summary>
        /// <param name="query"> The query to search for </param>
        /// <param name="n"> Return only the top n results </param>
        /// <param name="sort"> The <seealso cref="Lucene.Net.Search.Sort"/> object </param>
        /// <returns> The top docs, sorted according to the supplied <seealso cref="Lucene.Net.Search.Sort"/> instance </returns>
        /// <exception cref="IOException"> if there is a low-level I/O error </exception>
        public virtual TopFieldDocs Search(Query query, int n, Sort sort)
        {
            return Search(CreateNormalizedWeight(query), n, sort, false, false);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code> where all results are after a previous
        /// result (<code>after</code>).
        /// <p>
        /// By passing the bottom result from a previous page as <code>after</code>,
        /// this method can be used for efficient 'deep-paging' across potentially
        /// large result sets.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs SearchAfter(ScoreDoc after, Query query, int n, Sort sort)
        {
            if (after != null && !(after is FieldDoc))
            {
                // TODO: if we fix type safety of TopFieldDocs we can
                // remove this
                throw new System.ArgumentException("after must be a FieldDoc; got " + after);
            }
            return Search(CreateNormalizedWeight(query), (FieldDoc)after, n, sort, true, false, false);
        }

        /// <summary>
        /// Finds the top <code>n</code>
        /// hits for <code>query</code> where all results are after a previous
        /// result (<code>after</code>), allowing control over
        /// whether hit scores and max score should be computed.
        /// <p>
        /// By passing the bottom result from a previous page as <code>after</code>,
        /// this method can be used for efficient 'deep-paging' across potentially
        /// large result sets.  If <code>doDocScores</code> is <code>true</code>
        /// then the score of each hit will be computed and
        /// returned.  If <code>doMaxScore</code> is
        /// <code>true</code> then the maximum score over all
        /// collected hits will be computed.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual TopDocs SearchAfter(ScoreDoc after, Query query, Filter filter, int n, Sort sort, bool doDocScores, bool doMaxScore)
        {
            if (after != null && !(after is FieldDoc))
            {
                // TODO: if we fix type safety of TopFieldDocs we can
                // remove this
                throw new System.ArgumentException("after must be a FieldDoc; got " + after);
            }
            return Search(CreateNormalizedWeight(WrapFilter(query, filter)), (FieldDoc)after, n, sort, true, doDocScores, doMaxScore);
        }

        /// <summary>
        /// Expert: Low-level search implementation.  Finds the top <code>n</code>
        /// hits for <code>query</code>, applying <code>filter</code> if non-null.
        ///
        /// <p>Applications should usually call <seealso cref="IndexSearcher#search(Query,int)"/> or
        /// <seealso cref="IndexSearcher#search(Query,Filter,int)"/> instead. </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        protected internal virtual TopDocs Search(Weight weight, ScoreDoc after, int nDocs)
        {
            int limit = Reader.MaxDoc();
            if (limit == 0)
            {
                limit = 1;
            }
            if (after != null && after.Doc >= limit)
            {
                throw new System.ArgumentException("after.doc exceeds the number of documents in the reader: after.doc=" + after.Doc + " limit=" + limit);
            }
            nDocs = Math.Min(nDocs, limit);

            if (Executor == null)
            {
                return Search(LeafContexts, weight, after, nDocs);
            }
            else
            {
                HitQueue hq = new HitQueue(nDocs, false);
                ReentrantLock @lock = new ReentrantLock();
                ExecutionHelper<TopDocs> runner = new ExecutionHelper<TopDocs>(Executor);

                for (int i = 0; i < LeafSlices.Length; i++) // search each sub
                {
                    runner.Submit(new SearcherCallableNoSort(@lock, this, LeafSlices[i], weight, after, nDocs, hq));
                }

                int totalHits = 0;
                float maxScore = float.NegativeInfinity;
                foreach (TopDocs topDocs in runner)
                {
                    if (topDocs.TotalHits != 0)
                    {
                        totalHits += topDocs.TotalHits;
                        maxScore = Math.Max(maxScore, topDocs.MaxScore);
                    }
                }

                ScoreDoc[] scoreDocs = new ScoreDoc[hq.Size()];
                for (int i = hq.Size() - 1; i >= 0; i--) // put docs in array
                {
                    scoreDocs[i] = hq.Pop();
                }

                return new TopDocs(totalHits, scoreDocs, maxScore);
            }
        }

        /// <summary>
        /// Expert: Low-level search implementation.  Finds the top <code>n</code>
        /// hits for <code>query</code>.
        ///
        /// <p>Applications should usually call <seealso cref="IndexSearcher#search(Query,int)"/> or
        /// <seealso cref="IndexSearcher#search(Query,Filter,int)"/> instead. </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        protected internal virtual TopDocs Search(IList<AtomicReaderContext> leaves, Weight weight, ScoreDoc after, int nDocs)
        {
            // single thread
            int limit = Reader.MaxDoc();
            if (limit == 0)
            {
                limit = 1;
            }
            nDocs = Math.Min(nDocs, limit);
            TopScoreDocCollector collector = TopScoreDocCollector.Create(nDocs, after, !weight.ScoresDocsOutOfOrder());
            Search(leaves, weight, collector);
            return collector.TopDocs();
        }

        /// <summary>
        /// Expert: Low-level search implementation with arbitrary
        /// sorting and control over whether hit scores and max
        /// score should be computed.  Finds
        /// the top <code>n</code> hits for <code>query</code> and sorting the hits
        /// by the criteria in <code>sort</code>.
        ///
        /// <p>Applications should usually call {@link
        /// IndexSearcher#search(Query,Filter,int,Sort)} instead.
        /// </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        protected internal virtual TopFieldDocs Search(Weight weight, int nDocs, Sort sort, bool doDocScores, bool doMaxScore)
        {
            return Search(weight, null, nDocs, sort, true, doDocScores, doMaxScore);
        }

        /// <summary>
        /// Just like <seealso cref="#search(Weight, int, Sort, boolean, boolean)"/>, but you choose
        /// whether or not the fields in the returned <seealso cref="FieldDoc"/> instances should
        /// be set by specifying fillFields.
        /// </summary>
        protected internal virtual TopFieldDocs Search(Weight weight, FieldDoc after, int nDocs, Sort sort, bool fillFields, bool doDocScores, bool doMaxScore)
        {
            if (sort == null)
            {
                throw new System.NullReferenceException("Sort must not be null");
            }

            int limit = Reader.MaxDoc();
            if (limit == 0)
            {
                limit = 1;
            }
            nDocs = Math.Min(nDocs, limit);

            if (Executor == null)
            {
                // use all leaves here!
                return Search(LeafContexts, weight, after, nDocs, sort, fillFields, doDocScores, doMaxScore);
            }
            else
            {
                TopFieldCollector topCollector = TopFieldCollector.Create(sort, nDocs, after, fillFields, doDocScores, doMaxScore, false);

                ReentrantLock @lock = new ReentrantLock();
                ExecutionHelper<TopFieldDocs> runner = new ExecutionHelper<TopFieldDocs>(Executor);
                for (int i = 0; i < LeafSlices.Length; i++) // search each leaf slice
                {
                    runner.Submit(new SearcherCallableWithSort(@lock, this, LeafSlices[i], weight, after, nDocs, topCollector, sort, doDocScores, doMaxScore));
                }
                int totalHits = 0;
                float maxScore = float.NegativeInfinity;
                foreach (TopFieldDocs topFieldDocs in runner)
                {
                    if (topFieldDocs.TotalHits != 0)
                    {
                        totalHits += topFieldDocs.TotalHits;
                        maxScore = Math.Max(maxScore, topFieldDocs.MaxScore);
                    }
                }

                TopFieldDocs topDocs = (TopFieldDocs)topCollector.TopDocs();

                return new TopFieldDocs(totalHits, topDocs.ScoreDocs, topDocs.Fields, topDocs.MaxScore);
            }
        }

        /// <summary>
        /// Just like <seealso cref="#search(Weight, int, Sort, boolean, boolean)"/>, but you choose
        /// whether or not the fields in the returned <seealso cref="FieldDoc"/> instances should
        /// be set by specifying fillFields.
        /// </summary>
        protected internal virtual TopFieldDocs Search(IList<AtomicReaderContext> leaves, Weight weight, FieldDoc after, int nDocs, Sort sort, bool fillFields, bool doDocScores, bool doMaxScore)
        {
            // single thread
            int limit = Reader.MaxDoc();
            if (limit == 0)
            {
                limit = 1;
            }
            nDocs = Math.Min(nDocs, limit);

            TopFieldCollector collector = TopFieldCollector.Create(sort, nDocs, after, fillFields, doDocScores, doMaxScore, !weight.ScoresDocsOutOfOrder());
            Search(leaves, weight, collector);
            return (TopFieldDocs)collector.TopDocs();
        }

        /// <summary>
        /// Lower-level search API.
        ///
        /// <p>
        /// <seealso cref="Collector#collect(int)"/> is called for every document. <br>
        ///
        /// <p>
        /// NOTE: this method executes the searches on all given leaves exclusively.
        /// To search across all the searchers leaves use <seealso cref="#leafContexts"/>.
        /// </summary>
        /// <param name="leaves">
        ///          the searchers leaves to execute the searches on </param>
        /// <param name="weight">
        ///          to match documents </param>
        /// <param name="collector">
        ///          to receive hits </param>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        protected internal virtual void Search(IList<AtomicReaderContext> leaves, Weight weight, Collector collector)
        {
            // TODO: should we make this
            // threaded...?  the Collector could be sync'd?
            // always use single thread:
            foreach (AtomicReaderContext ctx in leaves) // search each subreader
            {
                try
                {
                    collector.NextReader = ctx;
                }
                catch (CollectionTerminatedException)
                {
                    // there is no doc of interest in this reader context
                    // continue with the following leaf
                    continue;
                }
                BulkScorer scorer = weight.BulkScorer(ctx, !collector.AcceptsDocsOutOfOrder(), ((AtomicReader)ctx.Reader()).LiveDocs);
                if (scorer != null)
                {
                    try
                    {
                        scorer.Score(collector);
                    }
                    catch (CollectionTerminatedException)
                    {
                        // collection was terminated prematurely
                        // continue with the following leaf
                    }
                }
            }
        }

        /// <summary>
        /// Expert: called to re-write queries into primitive queries. </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        public virtual Query Rewrite(Query original)
        {
            Query query = original;
            for (Query rewrittenQuery = query.Rewrite(Reader); rewrittenQuery != query; rewrittenQuery = query.Rewrite(Reader))
            {
                query = rewrittenQuery;
            }
            return query;
        }

        /// <summary>
        /// Returns an Explanation that describes how <code>doc</code> scored against
        /// <code>query</code>.
        ///
        /// <p>this is intended to be used in developing Similarity implementations,
        /// and, for good performance, should not be displayed with every hit.
        /// Computing an explanation is as expensive as executing the query over the
        /// entire index.
        /// </summary>
        public virtual Explanation Explain(Query query, int doc)
        {
            return Explain(CreateNormalizedWeight(query), doc);
        }

        /// <summary>
        /// Expert: low-level implementation method
        /// Returns an Explanation that describes how <code>doc</code> scored against
        /// <code>weight</code>.
        ///
        /// <p>this is intended to be used in developing Similarity implementations,
        /// and, for good performance, should not be displayed with every hit.
        /// Computing an explanation is as expensive as executing the query over the
        /// entire index.
        /// <p>Applications should call <seealso cref="IndexSearcher#explain(Query, int)"/>. </summary>
        /// <exception cref="BooleanQuery.TooManyClauses"> If a query would exceed
        ///         <seealso cref="BooleanQuery#getMaxClauseCount()"/> clauses. </exception>
        protected internal virtual Explanation Explain(Weight weight, int doc)
        {
            int n = ReaderUtil.SubIndex(doc, LeafContexts);
            AtomicReaderContext ctx = LeafContexts[n];
            int deBasedDoc = doc - ctx.DocBase;

            return weight.Explain(ctx, deBasedDoc);
        }

        /// <summary>
        /// Creates a normalized weight for a top-level <seealso cref="Query"/>.
        /// The query is rewritten by this method and <seealso cref="Query#createWeight"/> called,
        /// afterwards the <seealso cref="Weight"/> is normalized. The returned {@code Weight}
        /// can then directly be used to get a <seealso cref="Scorer"/>.
        /// @lucene.internal
        /// </summary>
        public virtual Weight CreateNormalizedWeight(Query query)
        {
            query = Rewrite(query);
            Weight weight = query.CreateWeight(this);
            float v = weight.ValueForNormalization;
            float norm = Similarity.QueryNorm(v);
            if (float.IsInfinity(norm) || float.IsNaN(norm))
            {
                norm = 1.0f;
            }
            weight.Normalize(norm, 1.0f);
            return weight;
        }

        /// <summary>
        /// Returns this searchers the top-level <seealso cref="IndexReaderContext"/>. </summary>
        /// <seealso cref= IndexReader#getContext() </seealso>
        /* sugar for #getReader().getTopReaderContext() */

        public virtual IndexReaderContext TopReaderContext
        {
            get
            {
                return ReaderContext;
            }
        }

        /// <summary>
        /// A thread subclass for searching a single searchable
        /// </summary>
        private sealed class SearcherCallableNoSort : ICallable<TopDocs>
        {
            internal readonly ReentrantLock @lock;
            internal readonly IndexSearcher Searcher;
            internal readonly Weight Weight;
            internal readonly ScoreDoc After;
            internal readonly int NDocs;
            internal readonly HitQueue Hq;
            internal readonly LeafSlice Slice;

            public SearcherCallableNoSort(ReentrantLock @lock, IndexSearcher searcher, LeafSlice slice, Weight weight, ScoreDoc after, int nDocs, HitQueue hq)
            {
                this.@lock = @lock;
                this.Searcher = searcher;
                this.Weight = weight;
                this.After = after;
                this.NDocs = nDocs;
                this.Hq = hq;
                this.Slice = slice;
            }

            public TopDocs Call()
            {
                TopDocs docs = Searcher.Search(Arrays.AsList(Slice.Leaves), Weight, After, NDocs);
                ScoreDoc[] scoreDocs = docs.ScoreDocs;
                //it would be so nice if we had a thread-safe insert
                @lock.Lock();
                try
                {
                    for (int j = 0; j < scoreDocs.Length; j++) // merge scoreDocs into hq
                    {
                        ScoreDoc scoreDoc = scoreDocs[j];
                        if (scoreDoc == Hq.InsertWithOverflow(scoreDoc))
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    @lock.Unlock();
                }
                return docs;
            }
        }

        /// <summary>
        /// A thread subclass for searching a single searchable
        /// </summary>
        private sealed class SearcherCallableWithSort : ICallable<TopFieldDocs>
        {
            internal readonly ReentrantLock @lock;
            internal readonly IndexSearcher Searcher;
            internal readonly Weight Weight;
            internal readonly int NDocs;
            internal readonly TopFieldCollector Hq;
            internal readonly Sort Sort;
            internal readonly LeafSlice Slice;
            internal readonly FieldDoc After;
            internal readonly bool DoDocScores;
            internal readonly bool DoMaxScore;

            public SearcherCallableWithSort(ReentrantLock @lock, IndexSearcher searcher, LeafSlice slice, Weight weight, FieldDoc after, int nDocs, TopFieldCollector hq, Sort sort, bool doDocScores, bool doMaxScore)
            {
                this.@lock = @lock;
                this.Searcher = searcher;
                this.Weight = weight;
                this.NDocs = nDocs;
                this.Hq = hq;
                this.Sort = sort;
                this.Slice = slice;
                this.After = after;
                this.DoDocScores = doDocScores;
                this.DoMaxScore = doMaxScore;
            }

            internal readonly FakeScorer FakeScorer = new FakeScorer();

            public TopFieldDocs Call()
            {
                Debug.Assert(Slice.Leaves.Length == 1);
                TopFieldDocs docs = Searcher.Search(Arrays.AsList(Slice.Leaves), Weight, After, NDocs, Sort, true, DoDocScores || Sort.NeedsScores(), DoMaxScore);
                @lock.Lock();
                try
                {
                    AtomicReaderContext ctx = Slice.Leaves[0];
                    int @base = ctx.DocBase;
                    Hq.NextReader = ctx;
                    Hq.Scorer = FakeScorer;
                    foreach (ScoreDoc scoreDoc in docs.ScoreDocs)
                    {
                        FakeScorer.doc = scoreDoc.Doc - @base;
                        FakeScorer.score = scoreDoc.Score;
                        Hq.Collect(scoreDoc.Doc - @base);
                    }

                    // Carry over maxScore from sub:
                    if (DoMaxScore && docs.MaxScore > Hq.MaxScore)
                    {
                        Hq.MaxScore = docs.MaxScore;
                    }
                }
                finally
                {
                    @lock.Unlock();
                }
                return docs;
            }
        }

        /// <summary>
        /// A helper class that wraps a <seealso cref="CompletionService"/> and provides an
        /// iterable interface to the completed <seealso cref="Callable"/> instances.
        /// </summary>
        /// @param <T>
        ///          the type of the <seealso cref="Callable"/> return value </param>
        private sealed class ExecutionHelper<T> : IEnumerator<T>, IEnumerable<T>
        {
            internal readonly ICompletionService<T> Service;
            internal int NumTasks;
            private T current;

            internal ExecutionHelper(TaskScheduler executor)
            {
                this.Service = new TaskSchedulerCompletionService<T>(executor);
            }

            public T Current
            {
                get
                {
                    return current;
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return current; }
            }

            public void Dispose()
            {
            }

            /*public override bool HasNext()
            {
              return NumTasks > 0;
            }*/

            public void Submit(ICallable<T> task)
            {
                this.Service.Submit(task);
                ++NumTasks;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            /*public override T Next()
            {
              if (!this.HasNext())
              {
                throw new NoSuchElementException("next() is called but hasNext() returned false");
              }
              try
              {
                return Service.Take().Get();
              }
              catch (ThreadInterruptedException e)
              {
                throw new ThreadInterruptedException(e);
              }
              catch (ExecutionException e)
              {
                throw new Exception(e);
              }
              finally
              {
                --NumTasks;
              }
            }*/

            public bool MoveNext()
            {
                if (NumTasks > 0)
                {
                    try
                    {
                        current = Service.Take().Result;
                    }
                    finally
                    {
                        --NumTasks;
                    }
                }

                return false;
            }

            /*public override void Remove()
            {
              throw new System.NotSupportedException();
            }*/

            public IEnumerator<T> GetEnumerator()
            {
                // use the shortcut here - this is only used in a private context
                return this;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this;
            }
        }

        /// <summary>
        /// A class holding a subset of the <seealso cref="IndexSearcher"/>s leaf contexts to be
        /// executed within a single thread.
        ///
        /// @lucene.experimental
        /// </summary>
        public class LeafSlice
        {
            internal readonly AtomicReaderContext[] Leaves;

            public LeafSlice(params AtomicReaderContext[] leaves)
            {
                this.Leaves = leaves;
            }
        }

        public override string ToString()
        {
            return "IndexSearcher(" + Reader + "; executor=" + Executor + ")";
        }

        /// <summary>
        /// Returns <seealso cref="TermStatistics"/> for a term.
        ///
        /// this can be overridden for example, to return a term's statistics
        /// across a distributed collection.
        /// @lucene.experimental
        /// </summary>
        public virtual TermStatistics TermStatistics(Term term, TermContext context)
        {
            return new TermStatistics(term.Bytes(), context.DocFreq, context.TotalTermFreq());
        }

        /// <summary>
        /// Returns <seealso cref="CollectionStatistics"/> for a field.
        ///
        /// this can be overridden for example, to return a field's statistics
        /// across a distributed collection.
        /// @lucene.experimental
        /// </summary>
        public virtual CollectionStatistics CollectionStatistics(string field)
        {
            int docCount;
            long sumTotalTermFreq;
            long sumDocFreq;

            Debug.Assert(field != null);

            Terms terms = MultiFields.GetTerms(Reader, field);
            if (terms == null)
            {
                docCount = 0;
                sumTotalTermFreq = 0;
                sumDocFreq = 0;
            }
            else
            {
                docCount = terms.DocCount;
                sumTotalTermFreq = terms.SumTotalTermFreq;
                sumDocFreq = terms.SumDocFreq;
            }
            return new CollectionStatistics(field, Reader.MaxDoc(), docCount, sumTotalTermFreq, sumDocFreq);
        }
    }
}