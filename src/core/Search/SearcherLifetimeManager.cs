using System;
using System.Collections.Generic;

namespace Lucene.Net.Search
{
    using Lucene.Net.Support;
    using AlreadyClosedException = Lucene.Net.Store.AlreadyClosedException;

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

    using DirectoryReader = Lucene.Net.Index.DirectoryReader;
    using IOUtils = Lucene.Net.Util.IOUtils;

    /// <summary>
    /// Keeps track of current plus old IndexSearchers, closing
    /// the old ones once they have timed out.
    ///
    /// Use it like this:
    ///
    /// <pre class="prettyprint">
    ///   SearcherLifetimeManager mgr = new SearcherLifetimeManager();
    /// </pre>
    ///
    /// Per search-request, if it's a "new" search request, then
    /// obtain the latest searcher you have (for example, by
    /// using <seealso cref="SearcherManager"/>), and then record this
    /// searcher:
    ///
    /// <pre class="prettyprint">
    ///   // Record the current searcher, and save the returend
    ///   // token into user's search results (eg as a  hidden
    ///   // HTML form field):
    ///   long token = mgr.record(searcher);
    /// </pre>
    ///
    /// When a follow-up search arrives, for example the user
    /// clicks next page, drills down/up, etc., take the token
    /// that you saved from the previous search and:
    ///
    /// <pre class="prettyprint">
    ///   // If possible, obtain the same searcher as the last
    ///   // search:
    ///   IndexSearcher searcher = mgr.acquire(token);
    ///   if (searcher != null) {
    ///     // Searcher is still here
    ///     try {
    ///       // do searching...
    ///     } finally {
    ///       mgr.release(searcher);
    ///       // Do not use searcher after this!
    ///       searcher = null;
    ///     }
    ///   } else {
    ///     // Searcher was pruned -- notify user session timed
    ///     // out, or, pull fresh searcher again
    ///   }
    /// </pre>
    ///
    /// Finally, in a separate thread, ideally the same thread
    /// that's periodically reopening your searchers, you should
    /// periodically prune old searchers:
    ///
    /// <pre class="prettyprint">
    ///   mgr.prune(new PruneByAge(600.0));
    /// </pre>
    ///
    /// <p><b>NOTE</b>: keeping many searchers around means
    /// you'll use more resources (open files, RAM) than a single
    /// searcher.  However, as long as you are using {@link
    /// DirectoryReader#openIfChanged(DirectoryReader)}, the searchers
    /// will usually share almost all segments and the added resource usage
    /// is contained.  When a large merge has completed, and
    /// you reopen, because that is a large change, the new
    /// searcher will use higher additional RAM than other
    /// searchers; but large merges don't complete very often and
    /// it's unlikely you'll hit two of them in your expiration
    /// window.  Still you should budget plenty of heap in the
    /// JVM to have a good safety margin.
    ///
    /// @lucene.experimental
    /// </summary>

    public class SearcherLifetimeManager : IDisposable
    {
        internal const double NANOS_PER_SEC = 1000000000.0;

        private class SearcherTracker : IComparable<SearcherTracker>, IDisposable
        {
            public readonly IndexSearcher Searcher;
            public readonly double RecordTimeSec;
            public readonly long Version;

            public SearcherTracker(IndexSearcher searcher)
            {
                this.Searcher = searcher;
                Version = ((DirectoryReader)searcher.IndexReader).Version;
                searcher.IndexReader.IncRef();
                // Use nanoTime not currentTimeMillis since it [in
                // theory] reduces risk from clock shift
                RecordTimeSec = DateTime.Now.ToFileTime() / 100.0d / NANOS_PER_SEC;
            }

            // Newer searchers are sort before older ones:
            public virtual int CompareTo(SearcherTracker other)
            {
                return other.RecordTimeSec.CompareTo(RecordTimeSec);
            }

            public void Dispose()
            {
                lock (this)
                {
                    Searcher.IndexReader.DecRef();
                }
            }
        }

        private volatile bool Closed;

        // TODO: we could get by w/ just a "set"; need to have
        // Tracker hash by its version and have compareTo(Long)
        // compare to its version
        private readonly ConcurrentHashMap<long, SearcherTracker> Searchers = new ConcurrentHashMap<long, SearcherTracker>();

        private void EnsureOpen()
        {
            if (Closed)
            {
                throw new AlreadyClosedException("this SearcherLifetimeManager instance is closed");
            }
        }

        /// <summary>
        /// Records that you are now using this IndexSearcher.
        ///  Always call this when you've obtained a possibly new
        ///  <seealso cref="IndexSearcher"/>, for example from {@link
        ///  SearcherManager}.  It's fine if you already passed the
        ///  same searcher to this method before.
        ///
        ///  <p>this returns the long token that you can later pass
        ///  to <seealso cref="#acquire"/> to retrieve the same IndexSearcher.
        ///  You should record this long token in the search results
        ///  sent to your user, such that if the user performs a
        ///  follow-on action (clicks next page, drills down, etc.)
        ///  the token is returned.
        /// </summary>
        public virtual long Record(IndexSearcher searcher)
        {
            EnsureOpen();
            // TODO: we don't have to use IR.getVersion to track;
            // could be risky (if it's buggy); we could get better
            // bug isolation if we assign our own private ID:
            long version = ((DirectoryReader)searcher.IndexReader).Version;
            SearcherTracker tracker = Searchers[version];
            if (tracker == null)
            {
                //System.out.println("RECORD version=" + version + " ms=" + System.currentTimeMillis());
                tracker = new SearcherTracker(searcher);
                if (Searchers.AddIfAbsent(version, tracker) != null)
                {
                    // Another thread beat us -- must decRef to undo
                    // incRef done by SearcherTracker ctor:
                    tracker.Dispose();
                }
            }
            else if (tracker.Searcher != searcher)
            {
                throw new System.ArgumentException("the provided searcher has the same underlying reader version yet the searcher instance differs from before (new=" + searcher + " vs old=" + tracker.Searcher);
            }

            return version;
        }

        /// <summary>
        /// Retrieve a previously recorded <seealso cref="IndexSearcher"/>, if it
        ///  has not yet been closed
        ///
        ///  <p><b>NOTE</b>: this may return null when the
        ///  requested searcher has already timed out.  When this
        ///  happens you should notify your user that their session
        ///  timed out and that they'll have to restart their
        ///  search.
        ///
        ///  <p>If this returns a non-null result, you must match
        ///  later call <seealso cref="#release"/> on this searcher, best
        ///  from a finally clause.
        /// </summary>
        public virtual IndexSearcher Acquire(long version)
        {
            EnsureOpen();
            SearcherTracker tracker = Searchers[version];
            if (tracker != null && tracker.Searcher.IndexReader.TryIncRef())
            {
                return tracker.Searcher;
            }

            return null;
        }

        /// <summary>
        /// Release a searcher previously obtained from {@link
        ///  #acquire}.
        ///
        /// <p><b>NOTE</b>: it's fine to call this after close.
        /// </summary>
        public virtual void Release(IndexSearcher s)
        {
            s.IndexReader.DecRef();
        }

        /// <summary>
        /// See <seealso cref="#prune"/>. </summary>
        public interface Pruner
        {
            /// <summary>
            /// Return true if this searcher should be removed. </summary>
            ///  <param name="ageSec"> how much time has passed since this
            ///         searcher was the current (live) searcher </param>
            ///  <param name="searcher"> Searcher
            ///  </param>
            bool DoPrune(double ageSec, IndexSearcher searcher);
        }

        /// <summary>
        /// Simple pruner that drops any searcher older by
        ///  more than the specified seconds, than the newest
        ///  searcher.
        /// </summary>
        public sealed class PruneByAge : Pruner
        {
            internal readonly double MaxAgeSec;

            public PruneByAge(double maxAgeSec)
            {
                if (maxAgeSec < 0)
                {
                    throw new System.ArgumentException("maxAgeSec must be > 0 (got " + maxAgeSec + ")");
                }
                this.MaxAgeSec = maxAgeSec;
            }

            public bool DoPrune(double ageSec, IndexSearcher searcher)
            {
                return ageSec > MaxAgeSec;
            }
        }

        /// <summary>
        /// Calls provided <seealso cref="Pruner"/> to prune entries.  The
        ///  entries are passed to the Pruner in sorted (newest to
        ///  oldest IndexSearcher) order.
        ///
        ///  <p><b>NOTE</b>: you must peridiocally call this, ideally
        ///  from the same background thread that opens new
        ///  searchers.
        /// </summary>
        public virtual void Prune(Pruner pruner)
        {
            lock (this)
            {
                // Cannot just pass searchers.values() to ArrayList ctor
                // (not thread-safe since the values can change while
                // ArrayList is init'ing itself); must instead iterate
                // ourselves:
                List<SearcherTracker> trackers = new List<SearcherTracker>();
                foreach (SearcherTracker tracker in Searchers.Values)
                {
                    trackers.Add(tracker);
                }
                trackers.Sort();
                double lastRecordTimeSec = 0.0;
                double now = DateTime.Now.ToFileTime() / 100.0d / NANOS_PER_SEC;
                foreach (SearcherTracker tracker in trackers)
                {
                    double ageSec;
                    if (lastRecordTimeSec == 0.0)
                    {
                        ageSec = 0.0;
                    }
                    else
                    {
                        ageSec = now - lastRecordTimeSec;
                    }
                    // First tracker is always age 0.0 sec, since it's
                    // still "live"; second tracker's age (= seconds since
                    // it was "live") is now minus first tracker's
                    // recordTime, etc:
                    if (pruner.DoPrune(ageSec, tracker.Searcher))
                    {
                        //System.out.println("PRUNE version=" + tracker.version + " age=" + ageSec + " ms=" + System.currentTimeMillis());
                        Searchers.Remove(tracker.Version);
                        tracker.Dispose();
                    }
                    lastRecordTimeSec = tracker.RecordTimeSec;
                }
            }
        }

        /// <summary>
        /// Close this to future searching; any searches still in
        ///  process in other threads won't be affected, and they
        ///  should still call <seealso cref="#release"/> after they are
        ///  done.
        ///
        ///  <p><b>NOTE</b>: you must ensure no other threads are
        ///  calling <seealso cref="#record"/> while you call close();
        ///  otherwise it's possible not all searcher references
        ///  will be freed.
        /// </summary>
        public void Dispose()
        {
            lock (this)
            {
                Closed = true;
                IList<SearcherTracker> toClose = new List<SearcherTracker>(Searchers.Values);

                // Remove up front in case exc below, so we don't
                // over-decRef on double-close:
                foreach (SearcherTracker tracker in toClose)
                {
                    Searchers.Remove(tracker.Version);
                }

                IOUtils.Close(toClose);

                // Make some effort to catch mis-use:
                if (Searchers.Count != 0)
                {
                    throw new InvalidOperationException("another thread called record while this SearcherLifetimeManager instance was being closed; not all searchers were closed");
                }
            }
        }
    }
}