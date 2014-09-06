using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Search
{
    using Lucene.Net.Support;

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

    using AtomicReader = Lucene.Net.Index.AtomicReader;
    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
    using Bits = Lucene.Net.Util.Bits;
    using RamUsageEstimator = Lucene.Net.Util.RamUsageEstimator;
    using WAH8DocIdSet = Lucene.Net.Util.WAH8DocIdSet;

    /// <summary>
    /// Wraps another <seealso cref="Filter"/>'s result and caches it.  The purpose is to allow
    /// filters to simply filter, and then wrap with this class
    /// to add caching.
    /// </summary>
    public class CachingWrapperFilter : Filter
    {
        private readonly Filter Filter_Renamed;

        //private readonly IDictionary<object, DocIdSet> Cache = Collections.synchronizedMap(new WeakHashMap<object, DocIdSet>());
        private readonly IDictionary<object, DocIdSet> Cache = new ConcurrentHashMapWrapper<object, DocIdSet>(new WeakDictionary<object, DocIdSet>());

        /// <summary>
        /// Wraps another filter's result and caches it. </summary>
        /// <param name="filter"> Filter to cache results of </param>
        public CachingWrapperFilter(Filter filter)
        {
            this.Filter_Renamed = filter;
        }

        /// <summary>
        /// Gets the contained filter. </summary>
        /// <returns> the contained filter. </returns>
        public virtual Filter Filter
        {
            get
            {
                return Filter_Renamed;
            }
        }

        /// <summary>
        ///  Provide the DocIdSet to be cached, using the DocIdSet provided
        ///  by the wrapped Filter. <p>this implementation returns the given <seealso cref="DocIdSet"/>,
        ///  if <seealso cref="DocIdSet#isCacheable"/> returns <code>true</code>, else it calls
        ///  <seealso cref="#cacheImpl(DocIdSetIterator,AtomicReader)"/>
        ///  <p>Note: this method returns <seealso cref="#EMPTY_DOCIDSET"/> if the given docIdSet
        ///  is <code>null</code> or if <seealso cref="DocIdSet#iterator()"/> return <code>null</code>. The empty
        ///  instance is use as a placeholder in the cache instead of the <code>null</code> value.
        /// </summary>
        protected internal virtual DocIdSet DocIdSetToCache(DocIdSet docIdSet, AtomicReader reader)
        {
            if (docIdSet == null)
            {
                // this is better than returning null, as the nonnull result can be cached
                return EMPTY_DOCIDSET;
            }
            else if (docIdSet.Cacheable)
            {
                return docIdSet;
            }
            else
            {
                DocIdSetIterator it = docIdSet.GetIterator();
                // null is allowed to be returned by iterator(),
                // in this case we wrap with the sentinel set,
                // which is cacheable.
                if (it == null)
                {
                    return EMPTY_DOCIDSET;
                }
                else
                {
                    return CacheImpl(it, reader);
                }
            }
        }

        /// <summary>
        /// Default cache implementation: uses <seealso cref="WAH8DocIdSet"/>.
        /// </summary>
        protected internal virtual DocIdSet CacheImpl(DocIdSetIterator iterator, AtomicReader reader)
        {
            WAH8DocIdSet.Builder builder = new WAH8DocIdSet.Builder();
            builder.Add(iterator);
            return builder.Build();
        }

        // for testing
        public int HitCount, MissCount;

        public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
        {
            AtomicReader reader = (AtomicReader)context.Reader();
            object key = reader.CoreCacheKey;

            DocIdSet docIdSet = Cache[key];
            if (docIdSet != null)
            {
                HitCount++;
            }
            else
            {
                MissCount++;
                docIdSet = DocIdSetToCache(Filter_Renamed.GetDocIdSet(context, null), reader);
                Debug.Assert(docIdSet.Cacheable);
                Cache[key] = docIdSet;
            }

            return docIdSet == EMPTY_DOCIDSET ? null : BitsFilteredDocIdSet.Wrap(docIdSet, acceptDocs);
        }

        public override string ToString()
        {
            return this.GetType().Name + "(" + Filter_Renamed + ")";
        }

        public override bool Equals(object o)
        {
            if (o == null || !this.GetType().Equals(o.GetType()))
            {
                return false;
            }
            CachingWrapperFilter other = (CachingWrapperFilter)o;
            return this.Filter_Renamed.Equals(other.Filter_Renamed);
        }

        public override int GetHashCode()
        {
            return (Filter_Renamed.GetHashCode() ^ this.GetType().GetHashCode());
        }

        /// <summary>
        /// An empty {@code DocIdSet} instance </summary>
        protected internal static readonly DocIdSet EMPTY_DOCIDSET = new DocIdSetAnonymousInnerClassHelper();

        private class DocIdSetAnonymousInnerClassHelper : DocIdSet
        {
            public DocIdSetAnonymousInnerClassHelper()
            {
            }

            public override DocIdSetIterator GetIterator()
            {
                return DocIdSetIterator.Empty();
            }

            public override bool Cacheable
            {
                get
                {
                    return true;
                }
            }

            // we explicitly provide no random access, as this filter is 100% sparse and iterator exits faster
            public override Bits GetBits()
            {
                return null;
            }
        }

        /// <summary>
        /// Returns total byte size used by cached filters. </summary>
        public virtual long SizeInBytes()
        {
            // Sync only to pull the current set of values:
            IList<DocIdSet> docIdSets;
            lock (Cache)
            {
                docIdSets = new List<DocIdSet>(Cache.Values);
            }

            long total = 0;
            foreach (DocIdSet dis in docIdSets)
            {
                total += RamUsageEstimator.SizeOf(dis);
            }

            return total;
        }
    }
}