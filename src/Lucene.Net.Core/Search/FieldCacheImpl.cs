using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Search
{
    using Lucene.Net.Support;
    using System.IO;

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
    using BinaryDocValues = Lucene.Net.Index.BinaryDocValues;
    using Bits = Lucene.Net.Util.Bits;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using DocsEnum = Lucene.Net.Index.DocsEnum;
    using DocTermOrds = Lucene.Net.Index.DocTermOrds;
    using DocValues = Lucene.Net.Index.DocValues;
    using FieldCacheSanityChecker = Lucene.Net.Util.FieldCacheSanityChecker;
    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using FixedBitSet = Lucene.Net.Util.FixedBitSet;
    using GrowableWriter = Lucene.Net.Util.Packed.GrowableWriter;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using MonotonicAppendingLongBuffer = Lucene.Net.Util.Packed.MonotonicAppendingLongBuffer;
    using NumericDocValues = Lucene.Net.Index.NumericDocValues;
    using PackedInts = Lucene.Net.Util.Packed.PackedInts;
    using PagedBytes = Lucene.Net.Util.PagedBytes;
    using SegmentReader = Lucene.Net.Index.SegmentReader;
    using SortedDocValues = Lucene.Net.Index.SortedDocValues;
    using SortedSetDocValues = Lucene.Net.Index.SortedSetDocValues;
    using Terms = Lucene.Net.Index.Terms;
    using TermsEnum = Lucene.Net.Index.TermsEnum;

    /// <summary>
    /// Expert: The default cache implementation, storing all values in memory.
    /// A WeakHashMap is used for storage.
    ///
    /// @since   lucene 1.4
    /// </summary>
    internal class FieldCacheImpl : FieldCache
    {
        private IDictionary<Type, Cache> Caches;

        internal FieldCacheImpl()
        {
            Init();

            //Have to do this here because no 'this' in class definition
            purgeCore = new CoreClosedListenerAnonymousInnerClassHelper(this);
            purgeReader = new ReaderClosedListenerAnonymousInnerClassHelper(this);
        }

        private void Init()
        {
            lock (this)
            {
                Caches = new Dictionary<Type, Cache>(9);
                Caches[typeof(sbyte)] = new ByteCache(this);
                Caches[typeof(short)] = new ShortCache(this);
                Caches[typeof(int)] = new IntCache(this);
                Caches[typeof(float)] = new FloatCache(this);
                Caches[typeof(long)] = new LongCache(this);
                Caches[typeof(double)] = new DoubleCache(this);
                Caches[typeof(BinaryDocValues)] = new BinaryDocValuesCache(this);
                Caches[typeof(SortedDocValues)] = new SortedDocValuesCache(this);
                Caches[typeof(DocTermOrds)] = new DocTermOrdsCache(this);
                Caches[typeof(DocsWithFieldCache)] = new DocsWithFieldCache(this);
            }
        }

        public virtual void PurgeAllCaches()
        {
            lock (this)
            {
                Init();
            }
        }

        public virtual void PurgeByCacheKey(object coreCacheKey)
        {
            lock (this)
            {
                foreach (Cache c in Caches.Values)
                {
                    c.PurgeByCacheKey(coreCacheKey);
                }
            }
        }

        public virtual FieldCache_Fields.CacheEntry[] CacheEntries
        {
            get
            {
                lock (this)
                {
                    IList<FieldCache_Fields.CacheEntry> result = new List<FieldCache_Fields.CacheEntry>(17);
                    foreach (KeyValuePair<Type, Cache> cacheEntry in Caches)
                    {
                        Cache cache = cacheEntry.Value;
                        Type cacheType = cacheEntry.Key;
                        lock (cache.ReaderCache)
                        {
                            foreach (KeyValuePair<object, IDictionary<CacheKey, object>> readerCacheEntry in cache.ReaderCache)
                            {
                                object readerKey = readerCacheEntry.Key;
                                if (readerKey == null)
                                {
                                    continue;
                                }
                                IDictionary<CacheKey, object> innerCache = readerCacheEntry.Value;
                                foreach (KeyValuePair<CacheKey, object> mapEntry in innerCache)
                                {
                                    CacheKey entry = mapEntry.Key;
                                    result.Add(new FieldCache_Fields.CacheEntry(readerKey, entry.Field, cacheType, entry.Custom, mapEntry.Value));
                                }
                            }
                        }
                    }
                    return result.ToArray();
                }
            }
        }

        // per-segment fieldcaches don't purge until the shared core closes.
        internal readonly SegmentReader.CoreClosedListener purgeCore;

        private class CoreClosedListenerAnonymousInnerClassHelper : SegmentReader.CoreClosedListener
        {
            private FieldCacheImpl OuterInstance;

            public CoreClosedListenerAnonymousInnerClassHelper(FieldCacheImpl outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public void OnClose(object ownerCoreCacheKey)
            {
                OuterInstance.PurgeByCacheKey(ownerCoreCacheKey);
            }
        }

        // composite/SlowMultiReaderWrapper fieldcaches don't purge until composite reader is closed.
        internal readonly IndexReader.ReaderClosedListener purgeReader;

        private class ReaderClosedListenerAnonymousInnerClassHelper : IndexReader.ReaderClosedListener
        {
            private FieldCacheImpl OuterInstance;

            public ReaderClosedListenerAnonymousInnerClassHelper(FieldCacheImpl outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public void OnClose(IndexReader owner)
            {
                Debug.Assert(owner is AtomicReader);
                OuterInstance.PurgeByCacheKey(((AtomicReader)owner).CoreCacheKey);
            }
        }

        private void InitReader(AtomicReader reader)
        {
            if (reader is SegmentReader)
            {
                ((SegmentReader)reader).AddCoreClosedListener(purgeCore);
            }
            else
            {
                // we have a slow reader of some sort, try to register a purge event
                // rather than relying on gc:
                object key = reader.CoreCacheKey;
                if (key is AtomicReader)
                {
                    ((AtomicReader)key).AddReaderClosedListener(purgeReader);
                }
                else
                {
                    // last chance
                    reader.AddReaderClosedListener(purgeReader);
                }
            }
        }

        /// <summary>
        /// Expert: Internal cache. </summary>
        internal abstract class Cache
        {
            internal Cache(FieldCacheImpl wrapper)
            {
                this.Wrapper = wrapper;
            }

            internal readonly FieldCacheImpl Wrapper;

            internal IDictionary<object, IDictionary<CacheKey, object>> ReaderCache = new WeakDictionary<object, IDictionary<CacheKey, object>>();

            protected internal abstract object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField);

            /// <summary>
            /// Remove this reader from the cache, if present. </summary>
            public virtual void PurgeByCacheKey(object coreCacheKey)
            {
                lock (ReaderCache)
                {
                    ReaderCache.Remove(coreCacheKey);
                }
            }

            /// <summary>
            /// Sets the key to the value for the provided reader;
            ///  if the key is already set then this doesn't change it.
            /// </summary>
            public virtual void Put(AtomicReader reader, CacheKey key, object value)
            {
                object readerKey = reader.CoreCacheKey;
                lock (ReaderCache)
                {
                    IDictionary<CacheKey, object> innerCache = ReaderCache[readerKey];
                    if (innerCache == null)
                    {
                        // First time this reader is using FieldCache
                        innerCache = new Dictionary<CacheKey, object>();
                        ReaderCache[readerKey] = innerCache;
                        Wrapper.InitReader(reader);
                    }
                    if (!innerCache.TryGetValue(key, out value))
                    {
                        innerCache[key] = value;
                    }
                    else
                    {
                        // Another thread beat us to it; leave the current
                        // value
                    }
                }
            }

            public virtual object Get(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                IDictionary<CacheKey, object> innerCache;
                object value;
                object readerKey = reader.CoreCacheKey;
                lock (ReaderCache)
                {
                    innerCache = ReaderCache[readerKey];
                    if (innerCache == null)
                    {
                        // First time this reader is using FieldCache
                        innerCache = new Dictionary<CacheKey, object>();
                        ReaderCache[readerKey] = innerCache;
                        Wrapper.InitReader(reader);
                        value = null;
                    }
                    else
                    {
                        innerCache.TryGetValue(key, out value);
                    }
                    if (value == null)
                    {
                        value = new FieldCache_Fields.CreationPlaceholder();
                        innerCache[key] = value;
                    }
                }
                if (value is FieldCache_Fields.CreationPlaceholder)
                {
                    lock (value)
                    {
                        FieldCache_Fields.CreationPlaceholder progress = (FieldCache_Fields.CreationPlaceholder)value;
                        if (progress.Value == null)
                        {
                            progress.Value = CreateValue(reader, key, setDocsWithField);
                            lock (ReaderCache)
                            {
                                innerCache[key] = progress.Value;
                            }

                            // Only check if key.custom (the parser) is
                            // non-null; else, we check twice for a single
                            // call to FieldCache.getXXX
                            if (key.Custom != null && Wrapper != null)
                            {
                                StreamWriter infoStream = Wrapper.InfoStream;
                                if (infoStream != null)
                                {
                                    PrintNewInsanity(infoStream, progress.Value);
                                }
                            }
                        }
                        return progress.Value;
                    }
                }
                return value;
            }

            internal virtual void PrintNewInsanity(StreamWriter infoStream, object value)
            {
                FieldCacheSanityChecker.Insanity[] insanities = FieldCacheSanityChecker.CheckSanity(Wrapper);
                for (int i = 0; i < insanities.Length; i++)
                {
                    FieldCacheSanityChecker.Insanity insanity = insanities[i];
                    FieldCache_Fields.CacheEntry[] entries = insanity.CacheEntries;
                    for (int j = 0; j < entries.Length; j++)
                    {
                        if (entries[j].Value == value)
                        {
                            // OK this insanity involves our entry
                            infoStream.WriteLine("WARNING: new FieldCache insanity created\nDetails: " + insanity.ToString());
                            infoStream.WriteLine("\nStack:\n");
                            infoStream.WriteLine(new Exception().StackTrace);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Expert: Every composite-key in the internal cache is of this type. </summary>
        internal class CacheKey
        {
            internal readonly string Field; // which Field
            internal readonly object Custom; // which custom comparator or parser

            /// <summary>
            /// Creates one of these objects for a custom comparator/parser. </summary>
            internal CacheKey(string field, object custom)
            {
                this.Field = field;
                this.Custom = custom;
            }

            /// <summary>
            /// Two of these are equal iff they reference the same field and type. </summary>
            public override bool Equals(object o)
            {
                if (o is CacheKey)
                {
                    CacheKey other = (CacheKey)o;
                    if (other.Field.Equals(Field))
                    {
                        if (other.Custom == null)
                        {
                            if (Custom == null)
                            {
                                return true;
                            }
                        }
                        else if (other.Custom.Equals(Custom))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// Composes a hashcode based on the field and type. </summary>
            public override int GetHashCode()
            {
                return Field.GetHashCode() ^ (Custom == null ? 0 : Custom.GetHashCode());
            }
        }

        private abstract class Uninvert
        {
            public Bits DocsWithField;

            public virtual void DoUninvert(AtomicReader reader, string field, bool setDocsWithField)
            {
                int maxDoc = reader.MaxDoc();
                Terms terms = reader.Terms(field);
                if (terms != null)
                {
                    if (setDocsWithField)
                    {
                        int termsDocCount = terms.DocCount;
                        Debug.Assert(termsDocCount <= maxDoc);
                        if (termsDocCount == maxDoc)
                        {
                            // Fast case: all docs have this field:
                            DocsWithField = new Lucene.Net.Util.Bits_MatchAllBits(maxDoc);
                            setDocsWithField = false;
                        }
                    }

                    TermsEnum termsEnum = TermsEnum(terms);

                    DocsEnum docs = null;
                    FixedBitSet docsWithField = null;
                    while (true)
                    {
                        BytesRef term = termsEnum.Next();
                        if (term == null)
                        {
                            break;
                        }
                        VisitTerm(term);
                        docs = termsEnum.Docs(null, docs, DocsEnum.FLAG_NONE);
                        while (true)
                        {
                            int docID = docs.NextDoc();
                            if (docID == DocIdSetIterator.NO_MORE_DOCS)
                            {
                                break;
                            }
                            VisitDoc(docID);
                            if (setDocsWithField)
                            {
                                if (docsWithField == null)
                                {
                                    // Lazy init
                                    this.DocsWithField = docsWithField = new FixedBitSet(maxDoc);
                                }
                                docsWithField.Set(docID);
                            }
                        }
                    }
                }
            }

            protected abstract TermsEnum TermsEnum(Terms terms);

            protected abstract void VisitTerm(BytesRef term);

            protected abstract void VisitDoc(int docID);
        }

        // null Bits means no docs matched
        internal virtual void SetDocsWithField(AtomicReader reader, string field, Bits docsWithField)
        {
            int maxDoc = reader.MaxDoc();
            Bits bits;
            if (docsWithField == null)
            {
                bits = new Lucene.Net.Util.Bits_MatchNoBits(maxDoc);
            }
            else if (docsWithField is FixedBitSet)
            {
                int numSet = ((FixedBitSet)docsWithField).Cardinality();
                if (numSet >= maxDoc)
                {
                    // The cardinality of the BitSet is maxDoc if all documents have a value.
                    Debug.Assert(numSet == maxDoc);
                    bits = new Lucene.Net.Util.Bits_MatchAllBits(maxDoc);
                }
                else
                {
                    bits = docsWithField;
                }
            }
            else
            {
                bits = docsWithField;
            }
            Caches[typeof(DocsWithFieldCache)].Put(reader, new CacheKey(field, null), bits);
        }

        // inherit javadocs
        public virtual FieldCache_Fields.Bytes GetBytes(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetBytes(reader, field, null, setDocsWithField);
        }

        public virtual FieldCache_Fields.Bytes GetBytes(AtomicReader reader, string field, FieldCache_Fields.IByteParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_BytesAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Bytes.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Bytes.EMPTY;
                }
                return (FieldCache_Fields.Bytes)Caches[typeof(sbyte)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_BytesAnonymousInnerClassHelper : FieldCache_Fields.Bytes
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_BytesAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override sbyte Get(int docID)
            {
                return (sbyte)ValuesIn.Get(docID);
            }
        }

        internal class BytesFromArray : FieldCache_Fields.Bytes
        {
            internal readonly sbyte[] Values;

            public BytesFromArray(sbyte[] values)
            {
                this.Values = values;
            }

            public override sbyte Get(int docID)
            {
                return Values[docID];
            }
        }

        internal sealed class ByteCache : Cache
        {
            internal ByteCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                int maxDoc = reader.MaxDoc();
                sbyte[] values;
                FieldCache_Fields.IByteParser parser = (FieldCache_Fields.IByteParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser = DEFAULT_SHORT_PARSER) so cache
                    // key includes DEFAULT_SHORT_PARSER:
                    return Wrapper.GetBytes(reader, key.Field, FieldCache_Fields.DEFAULT_BYTE_PARSER, setDocsWithField);
                }

                values = new sbyte[maxDoc];

                Uninvert u = new UninvertAnonymousInnerClassHelper(this, values, parser);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }

                return new BytesFromArray(values);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly ByteCache OuterInstance;

                private sbyte[] Values;
                private FieldCache_Fields.IByteParser Parser;

                public UninvertAnonymousInnerClassHelper(ByteCache outerInstance, sbyte[] values, FieldCache_Fields.IByteParser parser)
                {
                    this.OuterInstance = outerInstance;
                    this.Values = values;
                    this.Parser = parser;
                }

                private sbyte currentValue;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseByte(term);
                }

                protected override void VisitDoc(int docID)
                {
                    Values[docID] = currentValue;
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        // inherit javadocs
        public virtual FieldCache_Fields.Shorts GetShorts(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetShorts(reader, field, null, setDocsWithField);
        }

        // inherit javadocs
        public virtual FieldCache_Fields.Shorts GetShorts(AtomicReader reader, string field, FieldCache_Fields.IShortParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_ShortsAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Shorts.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Shorts.EMPTY;
                }
                return (FieldCache_Fields.Shorts)Caches[typeof(short)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_ShortsAnonymousInnerClassHelper : FieldCache_Fields.Shorts
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_ShortsAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override short Get(int docID)
            {
                return (short)ValuesIn.Get(docID);
            }
        }

        internal class ShortsFromArray : FieldCache_Fields.Shorts
        {
            internal readonly short[] Values;

            public ShortsFromArray(short[] values)
            {
                this.Values = values;
            }

            public override short Get(int docID)
            {
                return Values[docID];
            }
        }

        internal sealed class ShortCache : Cache
        {
            internal ShortCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                int maxDoc = reader.MaxDoc();
                short[] values;
                FieldCache_Fields.IShortParser parser = (FieldCache_Fields.IShortParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser = DEFAULT_SHORT_PARSER) so cache
                    // key includes DEFAULT_SHORT_PARSER:
                    return Wrapper.GetShorts(reader, key.Field, FieldCache_Fields.DEFAULT_SHORT_PARSER, setDocsWithField);
                }

                values = new short[maxDoc];
                Uninvert u = new UninvertAnonymousInnerClassHelper(this, values, parser);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }
                return new ShortsFromArray(values);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly ShortCache OuterInstance;

                private short[] Values;
                private FieldCache_Fields.IShortParser Parser;

                public UninvertAnonymousInnerClassHelper(ShortCache outerInstance, short[] values, FieldCache_Fields.IShortParser parser)
                {
                    this.OuterInstance = outerInstance;
                    this.Values = values;
                    this.Parser = parser;
                }

                private short currentValue;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseShort(term);
                }

                protected override void VisitDoc(int docID)
                {
                    Values[docID] = currentValue;
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        // inherit javadocs
        public virtual FieldCache_Fields.Ints GetInts(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetInts(reader, field, null, setDocsWithField);
        }

        public virtual FieldCache_Fields.Ints GetInts(AtomicReader reader, string field, FieldCache_Fields.IIntParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_IntsAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Ints.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Ints.EMPTY;
                }
                return (FieldCache_Fields.Ints)Caches[typeof(int)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_IntsAnonymousInnerClassHelper : FieldCache_Fields.Ints
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_IntsAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override int Get(int docID)
            {
                return (int)ValuesIn.Get(docID);
            }
        }

        internal class IntsFromArray : FieldCache_Fields.Ints
        {
            internal readonly PackedInts.Reader Values;
            internal readonly int MinValue;

            public IntsFromArray(PackedInts.Reader values, int minValue)
            {
                Debug.Assert(values.BitsPerValue <= 32);
                this.Values = values;
                this.MinValue = minValue;
            }

            public override int Get(int docID)
            {
                long delta = Values.Get(docID);
                return MinValue + (int)delta;
            }
        }

        private class HoldsOneThing<T>
        {
            internal T It;

            public virtual void Set(T it)
            {
                this.It = it;
            }

            public virtual T Get()
            {
                return It;
            }
        }

        private class GrowableWriterAndMinValue
        {
            internal GrowableWriterAndMinValue(GrowableWriter array, long minValue)
            {
                this.Writer = array;
                this.MinValue = minValue;
            }

            public GrowableWriter Writer;
            public long MinValue;
        }

        internal sealed class IntCache : Cache
        {
            internal IntCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                FieldCache_Fields.IIntParser parser = (FieldCache_Fields.IIntParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser =
                    // DEFAULT_INT_PARSER/NUMERIC_UTILS_INT_PARSER) so
                    // cache key includes
                    // DEFAULT_INT_PARSER/NUMERIC_UTILS_INT_PARSER:
                    try
                    {
                        return Wrapper.GetInts(reader, key.Field, FieldCache_Fields.DEFAULT_INT_PARSER, setDocsWithField);
                    }
                    catch (System.FormatException)
                    {
                        return Wrapper.GetInts(reader, key.Field, FieldCache_Fields.NUMERIC_UTILS_INT_PARSER, setDocsWithField);
                    }
                }

                HoldsOneThing<GrowableWriterAndMinValue> valuesRef = new HoldsOneThing<GrowableWriterAndMinValue>();

                Uninvert u = new UninvertAnonymousInnerClassHelper(this, reader, parser, valuesRef);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }
                GrowableWriterAndMinValue values = valuesRef.Get();
                if (values == null)
                {
                    return new IntsFromArray(new PackedInts.NullReader(reader.MaxDoc()), 0);
                }
                return new IntsFromArray(values.Writer.Mutable, (int)values.MinValue);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly IntCache OuterInstance;

                private AtomicReader Reader;
                private FieldCache_Fields.IIntParser Parser;
                private FieldCacheImpl.HoldsOneThing<GrowableWriterAndMinValue> ValuesRef;

                public UninvertAnonymousInnerClassHelper(IntCache outerInstance, AtomicReader reader, FieldCache_Fields.IIntParser parser, FieldCacheImpl.HoldsOneThing<GrowableWriterAndMinValue> valuesRef)
                {
                    this.OuterInstance = outerInstance;
                    this.Reader = reader;
                    this.Parser = parser;
                    this.ValuesRef = valuesRef;
                }

                private int minValue;
                private int currentValue;
                private GrowableWriter values;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseInt(term);
                    if (values == null)
                    {
                        // Lazy alloc so for the numeric field case
                        // (which will hit a System.FormatException
                        // when we first try the DEFAULT_INT_PARSER),
                        // we don't double-alloc:
                        int startBitsPerValue;
                        // Make sure than missing values (0) can be stored without resizing
                        if (currentValue < 0)
                        {
                            minValue = currentValue;
                            startBitsPerValue = PackedInts.BitsRequired((-minValue) & 0xFFFFFFFFL);
                        }
                        else
                        {
                            minValue = 0;
                            startBitsPerValue = PackedInts.BitsRequired(currentValue);
                        }
                        values = new GrowableWriter(startBitsPerValue, Reader.MaxDoc(), PackedInts.FAST);
                        if (minValue != 0)
                        {
                            values.Fill(0, values.Size(), (-minValue) & 0xFFFFFFFFL); // default value must be 0
                        }
                        ValuesRef.Set(new GrowableWriterAndMinValue(values, minValue));
                    }
                }

                protected override void VisitDoc(int docID)
                {
                    values.Set(docID, (currentValue - minValue) & 0xFFFFFFFFL);
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        public virtual Bits GetDocsWithField(AtomicReader reader, string field)
        {
            FieldInfo fieldInfo = reader.FieldInfos.FieldInfo(field);
            if (fieldInfo == null)
            {
                // field does not exist or has no value
                return new Lucene.Net.Util.Bits_MatchNoBits(reader.MaxDoc());
            }
            else if (fieldInfo.HasDocValues())
            {
                return reader.GetDocsWithField(field);
            }
            else if (!fieldInfo.Indexed)
            {
                return new Lucene.Net.Util.Bits_MatchNoBits(reader.MaxDoc());
            }
            return (Bits)Caches[typeof(DocsWithFieldCache)].Get(reader, new CacheKey(field, null), false);
        }

        internal sealed class DocsWithFieldCache : Cache
        {
            internal DocsWithFieldCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField) // ignored
            {
                string field = key.Field;
                int maxDoc = reader.MaxDoc();

                // Visit all docs that have terms for this field
                FixedBitSet res = null;
                Terms terms = reader.Terms(field);
                if (terms != null)
                {
                    int termsDocCount = terms.DocCount;
                    Debug.Assert(termsDocCount <= maxDoc);
                    if (termsDocCount == maxDoc)
                    {
                        // Fast case: all docs have this field:
                        return new Lucene.Net.Util.Bits_MatchAllBits(maxDoc);
                    }
                    TermsEnum termsEnum = terms.Iterator(null);
                    DocsEnum docs = null;
                    while (true)
                    {
                        BytesRef term = termsEnum.Next();
                        if (term == null)
                        {
                            break;
                        }
                        if (res == null)
                        {
                            // lazy init
                            res = new FixedBitSet(maxDoc);
                        }

                        docs = termsEnum.Docs(null, docs, DocsEnum.FLAG_NONE);
                        // TODO: use bulk API
                        while (true)
                        {
                            int docID = docs.NextDoc();
                            if (docID == DocIdSetIterator.NO_MORE_DOCS)
                            {
                                break;
                            }
                            res.Set(docID);
                        }
                    }
                }
                if (res == null)
                {
                    return new Lucene.Net.Util.Bits_MatchNoBits(maxDoc);
                }
                int numSet = res.Cardinality();
                if (numSet >= maxDoc)
                {
                    // The cardinality of the BitSet is maxDoc if all documents have a value.
                    Debug.Assert(numSet == maxDoc);
                    return new Lucene.Net.Util.Bits_MatchAllBits(maxDoc);
                }
                return res;
            }
        }

        public virtual FieldCache_Fields.Floats GetFloats(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetFloats(reader, field, null, setDocsWithField);
        }

        public virtual FieldCache_Fields.Floats GetFloats(AtomicReader reader, string field, FieldCache_Fields.IFloatParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_FloatsAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Floats.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Floats.EMPTY;
                }
                return (FieldCache_Fields.Floats)Caches[typeof(float)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_FloatsAnonymousInnerClassHelper : FieldCache_Fields.Floats
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_FloatsAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override float Get(int docID)
            {
                return Number.IntBitsToFloat((int)ValuesIn.Get(docID));
            }
        }

        internal class FloatsFromArray : FieldCache_Fields.Floats
        {
            internal readonly float[] Values;

            public FloatsFromArray(float[] values)
            {
                this.Values = values;
            }

            public override float Get(int docID)
            {
                return Values[docID];
            }
        }

        internal sealed class FloatCache : Cache
        {
            internal FloatCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                FieldCache_Fields.IFloatParser parser = (FieldCache_Fields.IFloatParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser =
                    // DEFAULT_FLOAT_PARSER/NUMERIC_UTILS_FLOAT_PARSER) so
                    // cache key includes
                    // DEFAULT_FLOAT_PARSER/NUMERIC_UTILS_FLOAT_PARSER:
                    try
                    {
                        return Wrapper.GetFloats(reader, key.Field, FieldCache_Fields.DEFAULT_FLOAT_PARSER, setDocsWithField);
                    }
                    catch (System.FormatException)
                    {
                        return Wrapper.GetFloats(reader, key.Field, FieldCache_Fields.NUMERIC_UTILS_FLOAT_PARSER, setDocsWithField);
                    }
                }

                HoldsOneThing<float[]> valuesRef = new HoldsOneThing<float[]>();

                Uninvert u = new UninvertAnonymousInnerClassHelper(this, reader, parser, valuesRef);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }

                float[] values = valuesRef.Get();
                if (values == null)
                {
                    values = new float[reader.MaxDoc()];
                }
                return new FloatsFromArray(values);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly FloatCache OuterInstance;

                private AtomicReader Reader;
                private FieldCache_Fields.IFloatParser Parser;
                private FieldCacheImpl.HoldsOneThing<float[]> ValuesRef;

                public UninvertAnonymousInnerClassHelper(FloatCache outerInstance, AtomicReader reader, FieldCache_Fields.IFloatParser parser, FieldCacheImpl.HoldsOneThing<float[]> valuesRef)
                {
                    this.OuterInstance = outerInstance;
                    this.Reader = reader;
                    this.Parser = parser;
                    this.ValuesRef = valuesRef;
                }

                private float currentValue;
                private float[] values;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseFloat(term);
                    if (values == null)
                    {
                        // Lazy alloc so for the numeric field case
                        // (which will hit a System.FormatException
                        // when we first try the DEFAULT_INT_PARSER),
                        // we don't double-alloc:
                        values = new float[Reader.MaxDoc()];
                        ValuesRef.Set(values);
                    }
                }

                protected override void VisitDoc(int docID)
                {
                    values[docID] = currentValue;
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        public virtual FieldCache_Fields.Longs GetLongs(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetLongs(reader, field, null, setDocsWithField);
        }

        public virtual FieldCache_Fields.Longs GetLongs(AtomicReader reader, string field, FieldCache_Fields.ILongParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_LongsAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Longs.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Longs.EMPTY;
                }
                return (FieldCache_Fields.Longs)Caches[typeof(long)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_LongsAnonymousInnerClassHelper : FieldCache_Fields.Longs
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_LongsAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override long Get(int docID)
            {
                return ValuesIn.Get(docID);
            }
        }

        internal class LongsFromArray : FieldCache_Fields.Longs
        {
            internal readonly PackedInts.Reader Values;
            internal readonly long MinValue;

            public LongsFromArray(PackedInts.Reader values, long minValue)
            {
                this.Values = values;
                this.MinValue = minValue;
            }

            public override long Get(int docID)
            {
                return MinValue + Values.Get(docID);
            }
        }

        internal sealed class LongCache : Cache
        {
            internal LongCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                FieldCache_Fields.ILongParser parser = (FieldCache_Fields.ILongParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser =
                    // DEFAULT_LONG_PARSER/NUMERIC_UTILS_LONG_PARSER) so
                    // cache key includes
                    // DEFAULT_LONG_PARSER/NUMERIC_UTILS_LONG_PARSER:
                    try
                    {
                        return Wrapper.GetLongs(reader, key.Field, FieldCache_Fields.DEFAULT_LONG_PARSER, setDocsWithField);
                    }
                    catch (System.FormatException)
                    {
                        return Wrapper.GetLongs(reader, key.Field, FieldCache_Fields.NUMERIC_UTILS_LONG_PARSER, setDocsWithField);
                    }
                }

                HoldsOneThing<GrowableWriterAndMinValue> valuesRef = new HoldsOneThing<GrowableWriterAndMinValue>();

                Uninvert u = new UninvertAnonymousInnerClassHelper(this, reader, parser, valuesRef);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }
                GrowableWriterAndMinValue values = valuesRef.Get();
                if (values == null)
                {
                    return new LongsFromArray(new PackedInts.NullReader(reader.MaxDoc()), 0L);
                }
                return new LongsFromArray(values.Writer.Mutable, values.MinValue);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly LongCache OuterInstance;

                private AtomicReader Reader;
                private FieldCache_Fields.ILongParser Parser;
                private FieldCacheImpl.HoldsOneThing<GrowableWriterAndMinValue> ValuesRef;

                public UninvertAnonymousInnerClassHelper(LongCache outerInstance, AtomicReader reader, FieldCache_Fields.ILongParser parser, FieldCacheImpl.HoldsOneThing<GrowableWriterAndMinValue> valuesRef)
                {
                    this.OuterInstance = outerInstance;
                    this.Reader = reader;
                    this.Parser = parser;
                    this.ValuesRef = valuesRef;
                }

                private long minValue;
                private long currentValue;
                private GrowableWriter values;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseLong(term);
                    if (values == null)
                    {
                        // Lazy alloc so for the numeric field case
                        // (which will hit a System.FormatException
                        // when we first try the DEFAULT_INT_PARSER),
                        // we don't double-alloc:
                        int startBitsPerValue;
                        // Make sure than missing values (0) can be stored without resizing
                        if (currentValue < 0)
                        {
                            minValue = currentValue;
                            startBitsPerValue = minValue == long.MinValue ? 64 : PackedInts.BitsRequired(-minValue);
                        }
                        else
                        {
                            minValue = 0;
                            startBitsPerValue = PackedInts.BitsRequired(currentValue);
                        }
                        values = new GrowableWriter(startBitsPerValue, Reader.MaxDoc(), PackedInts.FAST);
                        if (minValue != 0)
                        {
                            values.Fill(0, values.Size(), -minValue); // default value must be 0
                        }
                        ValuesRef.Set(new GrowableWriterAndMinValue(values, minValue));
                    }
                }

                protected override void VisitDoc(int docID)
                {
                    values.Set(docID, currentValue - minValue);
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        public virtual FieldCache_Fields.Doubles GetDoubles(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetDoubles(reader, field, null, setDocsWithField);
        }

        public virtual FieldCache_Fields.Doubles GetDoubles(AtomicReader reader, string field, FieldCache_Fields.IDoubleParser parser, bool setDocsWithField)
        {
            NumericDocValues valuesIn = reader.GetNumericDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return new FieldCache_DoublesAnonymousInnerClassHelper(this, valuesIn);
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return FieldCache_Fields.Doubles.EMPTY;
                }
                else if (info.HasDocValues())
                {
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return FieldCache_Fields.Doubles.EMPTY;
                }
                return (FieldCache_Fields.Doubles)Caches[typeof(double)].Get(reader, new CacheKey(field, parser), setDocsWithField);
            }
        }

        private class FieldCache_DoublesAnonymousInnerClassHelper : FieldCache_Fields.Doubles
        {
            private readonly FieldCacheImpl OuterInstance;

            private NumericDocValues ValuesIn;

            public FieldCache_DoublesAnonymousInnerClassHelper(FieldCacheImpl outerInstance, NumericDocValues valuesIn)
            {
                this.OuterInstance = outerInstance;
                this.ValuesIn = valuesIn;
            }

            public override double Get(int docID)
            {
                return BitConverter.Int64BitsToDouble(ValuesIn.Get(docID));
            }
        }

        internal class DoublesFromArray : FieldCache_Fields.Doubles
        {
            internal readonly double[] Values;

            public DoublesFromArray(double[] values)
            {
                this.Values = values;
            }

            public override double Get(int docID)
            {
                return Values[docID];
            }
        }

        internal sealed class DoubleCache : Cache
        {
            internal DoubleCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                FieldCache_Fields.IDoubleParser parser = (FieldCache_Fields.IDoubleParser)key.Custom;
                if (parser == null)
                {
                    // Confusing: must delegate to wrapper (vs simply
                    // setting parser =
                    // DEFAULT_DOUBLE_PARSER/NUMERIC_UTILS_DOUBLE_PARSER) so
                    // cache key includes
                    // DEFAULT_DOUBLE_PARSER/NUMERIC_UTILS_DOUBLE_PARSER:
                    try
                    {
                        return Wrapper.GetDoubles(reader, key.Field, FieldCache_Fields.DEFAULT_DOUBLE_PARSER, setDocsWithField);
                    }
                    catch (System.FormatException)
                    {
                        return Wrapper.GetDoubles(reader, key.Field, FieldCache_Fields.NUMERIC_UTILS_DOUBLE_PARSER, setDocsWithField);
                    }
                }

                HoldsOneThing<double[]> valuesRef = new HoldsOneThing<double[]>();

                Uninvert u = new UninvertAnonymousInnerClassHelper(this, reader, parser, valuesRef);

                u.DoUninvert(reader, key.Field, setDocsWithField);

                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, u.DocsWithField);
                }
                double[] values = valuesRef.Get();
                if (values == null)
                {
                    values = new double[reader.MaxDoc()];
                }
                return new DoublesFromArray(values);
            }

            private class UninvertAnonymousInnerClassHelper : Uninvert
            {
                private readonly DoubleCache OuterInstance;

                private AtomicReader Reader;
                private FieldCache_Fields.IDoubleParser Parser;
                private FieldCacheImpl.HoldsOneThing<double[]> ValuesRef;

                public UninvertAnonymousInnerClassHelper(DoubleCache outerInstance, AtomicReader reader, FieldCache_Fields.IDoubleParser parser, FieldCacheImpl.HoldsOneThing<double[]> valuesRef)
                {
                    this.OuterInstance = outerInstance;
                    this.Reader = reader;
                    this.Parser = parser;
                    this.ValuesRef = valuesRef;
                }

                private double currentValue;
                private double[] values;

                protected override void VisitTerm(BytesRef term)
                {
                    currentValue = Parser.ParseDouble(term);
                    if (values == null)
                    {
                        // Lazy alloc so for the numeric field case
                        // (which will hit a System.FormatException
                        // when we first try the DEFAULT_INT_PARSER),
                        // we don't double-alloc:
                        values = new double[Reader.MaxDoc()];
                        ValuesRef.Set(values);
                    }
                }

                protected override void VisitDoc(int docID)
                {
                    values[docID] = currentValue;
                }

                protected override TermsEnum TermsEnum(Terms terms)
                {
                    return Parser.TermsEnum(terms);
                }
            }
        }

        public class SortedDocValuesImpl : SortedDocValues
        {
            internal readonly PagedBytes.Reader Bytes;
            internal readonly MonotonicAppendingLongBuffer TermOrdToBytesOffset;
            internal readonly PackedInts.Reader DocToTermOrd;
            internal readonly int NumOrd;

            public SortedDocValuesImpl(PagedBytes.Reader bytes, MonotonicAppendingLongBuffer termOrdToBytesOffset, PackedInts.Reader docToTermOrd, int numOrd)
            {
                this.Bytes = bytes;
                this.DocToTermOrd = docToTermOrd;
                this.TermOrdToBytesOffset = termOrdToBytesOffset;
                this.NumOrd = numOrd;
            }

            public override int ValueCount
            {
                get
                {
                    return NumOrd;
                }
            }

            public override int GetOrd(int docID)
            {
                // Subtract 1, matching the 1+ord we did when
                // storing, so that missing values, which are 0 in the
                // packed ints, are returned as -1 ord:
                return (int)DocToTermOrd.Get(docID) - 1;
            }

            public override void LookupOrd(int ord, BytesRef ret)
            {
                if (ord < 0)
                {
                    throw new System.ArgumentException("ord must be >=0 (got ord=" + ord + ")");
                }
                Bytes.Fill(ret, TermOrdToBytesOffset.Get(ord));
            }
        }

        public virtual SortedDocValues GetTermsIndex(AtomicReader reader, string field)
        {
            return GetTermsIndex(reader, field, PackedInts.FAST);
        }

        public virtual SortedDocValues GetTermsIndex(AtomicReader reader, string field, float acceptableOverheadRatio)
        {
            SortedDocValues valuesIn = reader.GetSortedDocValues(field);
            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return valuesIn;
            }
            else
            {
                FieldInfo info = reader.FieldInfos.FieldInfo(field);
                if (info == null)
                {
                    return DocValues.EMPTY_SORTED;
                }
                else if (info.HasDocValues())
                {
                    // we don't try to build a sorted instance from numeric/binary doc
                    // values because dedup can be very costly
                    throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
                }
                else if (!info.Indexed)
                {
                    return DocValues.EMPTY_SORTED;
                }
                return (SortedDocValues)Caches[typeof(SortedDocValues)].Get(reader, new CacheKey(field, acceptableOverheadRatio), false);
            }
        }

        internal class SortedDocValuesCache : Cache
        {
            internal SortedDocValuesCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField) // ignored
            {
                int maxDoc = reader.MaxDoc();

                Terms terms = reader.Terms(key.Field);

                float acceptableOverheadRatio = (float)((float?)key.Custom);

                PagedBytes bytes = new PagedBytes(15);

                int startTermsBPV;

                int termCountHardLimit;
                if (maxDoc == int.MaxValue)
                {
                    termCountHardLimit = int.MaxValue;
                }
                else
                {
                    termCountHardLimit = maxDoc + 1;
                }

                // TODO: use Uninvert?
                if (terms != null)
                {
                    // Try for coarse estimate for number of bits; this
                    // should be an underestimate most of the time, which
                    // is fine -- GrowableWriter will reallocate as needed
                    long numUniqueTerms = terms.Size();
                    if (numUniqueTerms != -1L)
                    {
                        if (numUniqueTerms > termCountHardLimit)
                        {
                            // app is misusing the API (there is more than
                            // one term per doc); in this case we make best
                            // effort to load what we can (see LUCENE-2142)
                            numUniqueTerms = termCountHardLimit;
                        }

                        startTermsBPV = PackedInts.BitsRequired(numUniqueTerms);
                    }
                    else
                    {
                        startTermsBPV = 1;
                    }
                }
                else
                {
                    startTermsBPV = 1;
                }

                MonotonicAppendingLongBuffer termOrdToBytesOffset = new MonotonicAppendingLongBuffer();
                GrowableWriter docToTermOrd = new GrowableWriter(startTermsBPV, maxDoc, acceptableOverheadRatio);

                int termOrd = 0;

                // TODO: use Uninvert?

                if (terms != null)
                {
                    TermsEnum termsEnum = terms.Iterator(null);
                    DocsEnum docs = null;

                    while (true)
                    {
                        BytesRef term = termsEnum.Next();
                        if (term == null)
                        {
                            break;
                        }
                        if (termOrd >= termCountHardLimit)
                        {
                            break;
                        }

                        termOrdToBytesOffset.Add(bytes.CopyUsingLengthPrefix(term));
                        docs = termsEnum.Docs(null, docs, DocsEnum.FLAG_NONE);
                        while (true)
                        {
                            int docID = docs.NextDoc();
                            if (docID == DocIdSetIterator.NO_MORE_DOCS)
                            {
                                break;
                            }
                            // Store 1+ ord into packed bits
                            docToTermOrd.Set(docID, 1 + termOrd);
                        }
                        termOrd++;
                    }
                }
                termOrdToBytesOffset.Freeze();

                // maybe an int-only impl?
                return new SortedDocValuesImpl(bytes.Freeze(true), termOrdToBytesOffset, docToTermOrd.Mutable, termOrd);
            }
        }

        private class BinaryDocValuesImpl : BinaryDocValues
        {
            internal readonly PagedBytes.Reader Bytes;
            internal readonly PackedInts.Reader DocToOffset;

            public BinaryDocValuesImpl(PagedBytes.Reader bytes, PackedInts.Reader docToOffset)
            {
                this.Bytes = bytes;
                this.DocToOffset = docToOffset;
            }

            public override void Get(int docID, BytesRef ret)
            {
                int pointer = (int)DocToOffset.Get(docID);
                if (pointer == 0)
                {
                    ret.Bytes = BytesRef.EMPTY_BYTES;
                    ret.Offset = 0;
                    ret.Length = 0;
                }
                else
                {
                    Bytes.Fill(ret, pointer);
                }
            }
        }

        // TODO: this if DocTermsIndex was already created, we
        // should share it...
        public virtual BinaryDocValues GetTerms(AtomicReader reader, string field, bool setDocsWithField)
        {
            return GetTerms(reader, field, setDocsWithField, PackedInts.FAST);
        }

        public virtual BinaryDocValues GetTerms(AtomicReader reader, string field, bool setDocsWithField, float acceptableOverheadRatio)
        {
            BinaryDocValues valuesIn = reader.GetBinaryDocValues(field);
            if (valuesIn == null)
            {
                valuesIn = reader.GetSortedDocValues(field);
            }

            if (valuesIn != null)
            {
                // Not cached here by FieldCacheImpl (cached instead
                // per-thread by SegmentReader):
                return valuesIn;
            }

            FieldInfo info = reader.FieldInfos.FieldInfo(field);
            if (info == null)
            {
                return DocValues.EMPTY_BINARY;
            }
            else if (info.HasDocValues())
            {
                throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
            }
            else if (!info.Indexed)
            {
                return DocValues.EMPTY_BINARY;
            }

            return (BinaryDocValues)Caches[typeof(BinaryDocValues)].Get(reader, new CacheKey(field, acceptableOverheadRatio), setDocsWithField);
        }

        internal sealed class BinaryDocValuesCache : Cache
        {
            internal BinaryDocValuesCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField)
            {
                // TODO: would be nice to first check if DocTermsIndex
                // was already cached for this field and then return
                // that instead, to avoid insanity

                int maxDoc = reader.MaxDoc();
                Terms terms = reader.Terms(key.Field);

                float acceptableOverheadRatio = (float)((float?)key.Custom);

                int termCountHardLimit = maxDoc;

                // Holds the actual term data, expanded.
                PagedBytes bytes = new PagedBytes(15);

                int startBPV;

                if (terms != null)
                {
                    // Try for coarse estimate for number of bits; this
                    // should be an underestimate most of the time, which
                    // is fine -- GrowableWriter will reallocate as needed
                    long numUniqueTerms = terms.Size();
                    if (numUniqueTerms != -1L)
                    {
                        if (numUniqueTerms > termCountHardLimit)
                        {
                            numUniqueTerms = termCountHardLimit;
                        }
                        startBPV = PackedInts.BitsRequired(numUniqueTerms * 4);
                    }
                    else
                    {
                        startBPV = 1;
                    }
                }
                else
                {
                    startBPV = 1;
                }

                GrowableWriter docToOffset = new GrowableWriter(startBPV, maxDoc, acceptableOverheadRatio);

                // pointer==0 means not set
                bytes.CopyUsingLengthPrefix(new BytesRef());

                if (terms != null)
                {
                    int termCount = 0;
                    TermsEnum termsEnum = terms.Iterator(null);
                    DocsEnum docs = null;
                    while (true)
                    {
                        if (termCount++ == termCountHardLimit)
                        {
                            // app is misusing the API (there is more than
                            // one term per doc); in this case we make best
                            // effort to load what we can (see LUCENE-2142)
                            break;
                        }

                        BytesRef term = termsEnum.Next();
                        if (term == null)
                        {
                            break;
                        }
                        long pointer = bytes.CopyUsingLengthPrefix(term);
                        docs = termsEnum.Docs(null, docs, DocsEnum.FLAG_NONE);
                        while (true)
                        {
                            int docID = docs.NextDoc();
                            if (docID == DocIdSetIterator.NO_MORE_DOCS)
                            {
                                break;
                            }
                            docToOffset.Set(docID, pointer);
                        }
                    }
                }

                PackedInts.Reader offsetReader = docToOffset.Mutable;
                if (setDocsWithField)
                {
                    Wrapper.SetDocsWithField(reader, key.Field, new BitsAnonymousInnerClassHelper(this, maxDoc, offsetReader));
                }
                // maybe an int-only impl?
                return new BinaryDocValuesImpl(bytes.Freeze(true), offsetReader);
            }

            private class BitsAnonymousInnerClassHelper : Bits
            {
                private readonly BinaryDocValuesCache OuterInstance;

                private int MaxDoc;
                private PackedInts.Reader OffsetReader;

                public BitsAnonymousInnerClassHelper(BinaryDocValuesCache outerInstance, int maxDoc, PackedInts.Reader offsetReader)
                {
                    this.OuterInstance = outerInstance;
                    this.MaxDoc = maxDoc;
                    this.OffsetReader = offsetReader;
                }

                public virtual bool Get(int index)
                {
                    return OffsetReader.Get(index) != 0;
                }

                public virtual int Length()
                {
                    return MaxDoc;
                }
            }
        }

        // TODO: this if DocTermsIndex was already created, we
        // should share it...
        public virtual SortedSetDocValues GetDocTermOrds(AtomicReader reader, string field)
        {
            SortedSetDocValues dv = reader.GetSortedSetDocValues(field);
            if (dv != null)
            {
                return dv;
            }

            SortedDocValues sdv = reader.GetSortedDocValues(field);
            if (sdv != null)
            {
                return DocValues.Singleton(sdv);
            }

            FieldInfo info = reader.FieldInfos.FieldInfo(field);
            if (info == null)
            {
                return DocValues.EMPTY_SORTED_SET;
            }
            else if (info.HasDocValues())
            {
                throw new InvalidOperationException("Type mismatch: " + field + " was indexed as " + info.DocValuesType);
            }
            else if (!info.Indexed)
            {
                return DocValues.EMPTY_SORTED_SET;
            }

            DocTermOrds dto = (DocTermOrds)Caches[typeof(DocTermOrds)].Get(reader, new CacheKey(field, null), false);
            return dto.GetIterator(reader);
        }

        internal sealed class DocTermOrdsCache : Cache
        {
            internal DocTermOrdsCache(FieldCacheImpl wrapper)
                : base(wrapper)
            {
            }

            protected internal override object CreateValue(AtomicReader reader, CacheKey key, bool setDocsWithField) // ignored
            {
                return new DocTermOrds(reader, null, key.Field);
            }
        }

        private volatile StreamWriter infoStream;

        public virtual StreamWriter InfoStream
        {
            set
            {
                infoStream = value;
            }
            get
            {
                return infoStream;
            }
        }
    }
}