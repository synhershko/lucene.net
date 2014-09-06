using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Lucene.Net.Util;

namespace Lucene.Net.Search
{

    /*
    /// Copyright 2004 The Apache Software Foundation
    /// 
    /// Licensed under the Apache License, Version 2.0 (the "License");
    /// you may not use this file except in compliance with the License.
    /// You may obtain a copy of the License at
    /// 
    ///     http://www.apache.org/licenses/LICENSE-2.0
    /// 
    /// Unless required by applicable law or agreed to in writing, software
    /// distributed under the License is distributed on an "AS IS" BASIS,
    /// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    /// See the License for the specific language governing permissions and
    /// limitations under the License.
    */


    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using BinaryDocValuesField = Lucene.Net.Document.BinaryDocValuesField;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using Store = Lucene.Net.Document.Field.Store;
    using IntField = Lucene.Net.Document.IntField;
    using LongField = Lucene.Net.Document.LongField;
    using NumericDocValuesField = Lucene.Net.Document.NumericDocValuesField;
    using SortedDocValuesField = Lucene.Net.Document.SortedDocValuesField;
    using SortedSetDocValuesField = Lucene.Net.Document.SortedSetDocValuesField;
    using StoredField = Lucene.Net.Document.StoredField;
    using AtomicReader = Lucene.Net.Index.AtomicReader;
    using BinaryDocValues = Lucene.Net.Index.BinaryDocValues;
    using DirectoryReader = Lucene.Net.Index.DirectoryReader;
    using DocTermOrds = Lucene.Net.Index.DocTermOrds;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using IndexWriter = Lucene.Net.Index.IndexWriter;
    using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using SlowCompositeReaderWrapper = Lucene.Net.Index.SlowCompositeReaderWrapper;
    using SortedDocValues = Lucene.Net.Index.SortedDocValues;
    using SortedSetDocValues = Lucene.Net.Index.SortedSetDocValues;
    using TermsEnum = Lucene.Net.Index.TermsEnum;
    using Bytes = Lucene.Net.Search.FieldCache_Fields.Bytes;
    using Doubles = Lucene.Net.Search.FieldCache_Fields.Doubles;
    using Floats = Lucene.Net.Search.FieldCache_Fields.Floats;
    using Ints = Lucene.Net.Search.FieldCache_Fields.Ints;
    using Longs = Lucene.Net.Search.FieldCache_Fields.Longs;
    using Shorts = Lucene.Net.Search.FieldCache_Fields.Shorts;
    using Directory = Lucene.Net.Store.Directory;
    using Bits = Lucene.Net.Util.Bits;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using IOUtils = Lucene.Net.Util.IOUtils;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using TestUtil = Lucene.Net.Util.TestUtil;
    using Lucene.Net.Randomized.Generators;
    using Lucene.Net.Support;
    using NUnit.Framework;

    [TestFixture]
    public class TestFieldCache : LuceneTestCase
    {
        private static AtomicReader Reader;
        private static int NUM_DOCS;
        private static int NUM_ORDS;
        private static string[] UnicodeStrings;
        private static BytesRef[,] MultiValued;
        private static Directory Directory;

        [TestFixtureSetUp]
        public static void BeforeClass()
        {
            NUM_DOCS = AtLeast(500);
            NUM_ORDS = AtLeast(2);
            Directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), Directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMergePolicy(NewLogMergePolicy()));
            long theLong = long.MaxValue;
            double theDouble = double.MaxValue;
            sbyte theByte = sbyte.MaxValue;
            short theShort = short.MaxValue;
            int theInt = int.MaxValue;
            float theFloat = float.MaxValue;
            UnicodeStrings = new string[NUM_DOCS];
            MultiValued = new BytesRef[NUM_DOCS, NUM_ORDS];
            if (VERBOSE)
            {
                Console.WriteLine("TEST: setUp");
            }
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Document doc = new Document();
                doc.Add(NewStringField("theLong", Convert.ToString(theLong--), Field.Store.NO));
                doc.Add(NewStringField("theDouble", Convert.ToString(theDouble--), Field.Store.NO));
                doc.Add(NewStringField("theByte", Convert.ToString(theByte--), Field.Store.NO));
                doc.Add(NewStringField("theShort", Convert.ToString(theShort--), Field.Store.NO));
                doc.Add(NewStringField("theInt", Convert.ToString(theInt--), Field.Store.NO));
                doc.Add(NewStringField("theFloat", Convert.ToString(theFloat--), Field.Store.NO));
                if (i % 2 == 0)
                {
                    doc.Add(NewStringField("sparse", Convert.ToString(i), Field.Store.NO));
                }

                if (i % 2 == 0)
                {
                    doc.Add(new IntField("numInt", i, Field.Store.NO));
                }

                // sometimes skip the field:
                if (Random().Next(40) != 17)
                {
                    UnicodeStrings[i] = GenerateString(i);
                    doc.Add(NewStringField("theRandomUnicodeString", UnicodeStrings[i], Field.Store.YES));
                }

                // sometimes skip the field:
                if (Random().Next(10) != 8)
                {
                    for (int j = 0; j < NUM_ORDS; j++)
                    {
                        string newValue = GenerateString(i);
                        MultiValued[i, j] = new BytesRef(newValue);
                        doc.Add(NewStringField("theRandomUnicodeMultiValuedField", newValue, Field.Store.YES));
                    }
                    Array.Sort(MultiValued[i]);
                }
                writer.AddDocument(doc);
            }
            IndexReader r = writer.Reader;
            Reader = SlowCompositeReaderWrapper.Wrap(r);
            writer.Dispose();
        }

        [TestFixtureTearDown]
        public static void AfterClass()
        {
            Reader.Dispose();
            Reader = null;
            Directory.Dispose();
            Directory = null;
            UnicodeStrings = null;
            MultiValued = null;
        }

        [Test]
        public virtual void TestInfoStream()
        {
            try
            {
                FieldCache cache = FieldCache_Fields.DEFAULT;
                MemoryStream bos = new MemoryStream(1024);
                cache.InfoStream = new StreamWriter(bos.ToString(), false, IOUtils.CHARSET_UTF_8);
                cache.GetDoubles(Reader, "theDouble", false);
                cache.GetFloats(Reader, "theDouble", false);
                Assert.IsTrue(bos.ToString(/*IOUtils.UTF_8*/).IndexOf("WARNING") != -1);
            }
            finally
            {
                FieldCache_Fields.DEFAULT.PurgeAllCaches();
            }
        }

        [Test]
        public virtual void Test()
        {
            FieldCache cache = FieldCache_Fields.DEFAULT;
            FieldCache_Fields.Doubles doubles = cache.GetDoubles(Reader, "theDouble", Random().NextBoolean());
            Assert.AreSame(doubles, cache.GetDoubles(Reader, "theDouble", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(doubles, cache.GetDoubles(Reader, "theDouble", FieldCache_Fields.DEFAULT_DOUBLE_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(doubles.Get(i) == (double.MaxValue - i), doubles.Get(i) + " does not equal: " + (double.MaxValue - i));
            }

            FieldCache_Fields.Longs longs = cache.GetLongs(Reader, "theLong", Random().NextBoolean());
            Assert.AreSame(longs, cache.GetLongs(Reader, "theLong", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(longs, cache.GetLongs(Reader, "theLong", FieldCache_Fields.DEFAULT_LONG_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(longs.Get(i) == (long.MaxValue - i), longs.Get(i) + " does not equal: " + (long.MaxValue - i) + " i=" + i);
            }

            FieldCache_Fields.Bytes bytes = cache.GetBytes(Reader, "theByte", Random().NextBoolean());
            Assert.AreSame(bytes, cache.GetBytes(Reader, "theByte", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(bytes, cache.GetBytes(Reader, "theByte", FieldCache_Fields.DEFAULT_BYTE_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(bytes.Get(i) == (sbyte)(sbyte.MaxValue - i), bytes.Get(i) + " does not equal: " + (sbyte.MaxValue - i));
            }

            FieldCache_Fields.Shorts shorts = cache.GetShorts(Reader, "theShort", Random().NextBoolean());
            Assert.AreSame(shorts, cache.GetShorts(Reader, "theShort", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(shorts, cache.GetShorts(Reader, "theShort", FieldCache_Fields.DEFAULT_SHORT_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(shorts.Get(i) == (short)(short.MaxValue - i), shorts.Get(i) + " does not equal: " + (short.MaxValue - i));
            }

            FieldCache_Fields.Ints ints = cache.GetInts(Reader, "theInt", Random().NextBoolean());
            Assert.AreSame(ints, cache.GetInts(Reader, "theInt", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(ints, cache.GetInts(Reader, "theInt", FieldCache_Fields.DEFAULT_INT_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(ints.Get(i) == (int.MaxValue - i), ints.Get(i) + " does not equal: " + (int.MaxValue - i));
            }

            FieldCache_Fields.Floats floats = cache.GetFloats(Reader, "theFloat", Random().NextBoolean());
            Assert.AreSame(floats, cache.GetFloats(Reader, "theFloat", Random().NextBoolean()), "Second request to cache return same array");
            Assert.AreSame(floats, cache.GetFloats(Reader, "theFloat", FieldCache_Fields.DEFAULT_FLOAT_PARSER, Random().NextBoolean()), "Second request with explicit parser return same array");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                Assert.IsTrue(floats.Get(i) == (float.MaxValue - i), floats.Get(i) + " does not equal: " + (float.MaxValue - i));
            }

            Bits docsWithField = cache.GetDocsWithField(Reader, "theLong");
            Assert.AreSame(docsWithField, cache.GetDocsWithField(Reader, "theLong"), "Second request to cache return same array");
            Assert.IsTrue(docsWithField is Bits_MatchAllBits, "docsWithField(theLong) must be class Bits.MatchAllBits");
            Assert.IsTrue(docsWithField.Length() == NUM_DOCS, "docsWithField(theLong) Size: " + docsWithField.Length() + " is not: " + NUM_DOCS);
            for (int i = 0; i < docsWithField.Length(); i++)
            {
                Assert.IsTrue(docsWithField.Get(i));
            }

            docsWithField = cache.GetDocsWithField(Reader, "sparse");
            Assert.AreSame(docsWithField, cache.GetDocsWithField(Reader, "sparse"), "Second request to cache return same array");
            Assert.IsFalse(docsWithField is Bits_MatchAllBits, "docsWithField(sparse) must not be class Bits.MatchAllBits");
            Assert.IsTrue(docsWithField.Length() == NUM_DOCS, "docsWithField(sparse) Size: " + docsWithField.Length() + " is not: " + NUM_DOCS);
            for (int i = 0; i < docsWithField.Length(); i++)
            {
                Assert.AreEqual(i % 2 == 0, docsWithField.Get(i));
            }

            // getTermsIndex
            SortedDocValues termsIndex = cache.GetTermsIndex(Reader, "theRandomUnicodeString");
            Assert.AreSame(termsIndex, cache.GetTermsIndex(Reader, "theRandomUnicodeString"), "Second request to cache return same array");
            BytesRef br = new BytesRef();
            for (int i = 0; i < NUM_DOCS; i++)
            {
                BytesRef term;
                int ord = termsIndex.GetOrd(i);
                if (ord == -1)
                {
                    term = null;
                }
                else
                {
                    termsIndex.LookupOrd(ord, br);
                    term = br;
                }
                string s = term == null ? null : term.Utf8ToString();
                Assert.IsTrue(UnicodeStrings[i] == null || UnicodeStrings[i].Equals(s), "for doc " + i + ": " + s + " does not equal: " + UnicodeStrings[i]);
            }

            int nTerms = termsIndex.ValueCount;

            TermsEnum tenum = termsIndex.TermsEnum();
            BytesRef val = new BytesRef();
            for (int i = 0; i < nTerms; i++)
            {
                BytesRef val1 = tenum.Next();
                termsIndex.LookupOrd(i, val);
                // System.out.println("i="+i);
                Assert.AreEqual(val, val1);
            }

            // seek the enum around (note this isn't a great test here)
            int num = AtLeast(100);
            for (int i = 0; i < num; i++)
            {
                int k = Random().Next(nTerms);
                termsIndex.LookupOrd(k, val);
                Assert.AreEqual(TermsEnum.SeekStatus.FOUND, tenum.SeekCeil(val));
                Assert.AreEqual(val, tenum.Term());
            }

            for (int i = 0; i < nTerms; i++)
            {
                termsIndex.LookupOrd(i, val);
                Assert.AreEqual(TermsEnum.SeekStatus.FOUND, tenum.SeekCeil(val));
                Assert.AreEqual(val, tenum.Term());
            }

            // test bad field
            termsIndex = cache.GetTermsIndex(Reader, "bogusfield");

            // getTerms
            BinaryDocValues terms = cache.GetTerms(Reader, "theRandomUnicodeString", true);
            Assert.AreSame(terms, cache.GetTerms(Reader, "theRandomUnicodeString", true), "Second request to cache return same array");
            Bits bits = cache.GetDocsWithField(Reader, "theRandomUnicodeString");
            for (int i = 0; i < NUM_DOCS; i++)
            {
                terms.Get(i, br);
                BytesRef term;
                if (!bits.Get(i))
                {
                    term = null;
                }
                else
                {
                    term = br;
                }
                string s = term == null ? null : term.Utf8ToString();
                Assert.IsTrue(UnicodeStrings[i] == null || UnicodeStrings[i].Equals(s), "for doc " + i + ": " + s + " does not equal: " + UnicodeStrings[i]);
            }

            // test bad field
            terms = cache.GetTerms(Reader, "bogusfield", false);

            // getDocTermOrds
            SortedSetDocValues termOrds = cache.GetDocTermOrds(Reader, "theRandomUnicodeMultiValuedField");
            int numEntries = cache.CacheEntries.Length;
            // ask for it again, and check that we didnt create any additional entries:
            termOrds = cache.GetDocTermOrds(Reader, "theRandomUnicodeMultiValuedField");
            Assert.AreEqual(numEntries, cache.CacheEntries.Length);

            for (int i = 0; i < NUM_DOCS; i++)
            {
                termOrds.Document = i;
                // this will remove identical terms. A DocTermOrds doesn't return duplicate ords for a docId
                IList<BytesRef> values = new List<BytesRef>(new /*Linked*/HashSet<BytesRef>(Arrays.AsList(MultiValued[i])));
                foreach (BytesRef v in values)
                {
                    if (v == null)
                    {
                        // why does this test use null values... instead of an empty list: confusing
                        break;
                    }
                    long ord = termOrds.NextOrd();
                    Debug.Assert(ord != SortedSetDocValues.NO_MORE_ORDS);
                    BytesRef scratch = new BytesRef();
                    termOrds.LookupOrd(ord, scratch);
                    Assert.AreEqual(v, scratch);
                }
                Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, termOrds.NextOrd());
            }

            // test bad field
            termOrds = cache.GetDocTermOrds(Reader, "bogusfield");
            Assert.IsTrue(termOrds.ValueCount == 0);

            FieldCache_Fields.DEFAULT.PurgeByCacheKey(Reader.CoreCacheKey);
        }

        [Test]
        public virtual void TestEmptyIndex()
        {
            Directory dir = NewDirectory();
            IndexWriter writer = new IndexWriter(dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMaxBufferedDocs(500));
            writer.Dispose();
            IndexReader r = DirectoryReader.Open(dir);
            AtomicReader reader = SlowCompositeReaderWrapper.Wrap(r);
            FieldCache_Fields.DEFAULT.GetTerms(reader, "foobar", true);
            FieldCache_Fields.DEFAULT.GetTermsIndex(reader, "foobar");
            FieldCache_Fields.DEFAULT.PurgeByCacheKey(reader.CoreCacheKey);
            r.Dispose();
            dir.Dispose();
        }

        private static string GenerateString(int i)
        {
            string s = null;
            if (i > 0 && Random().Next(3) == 1)
            {
                // reuse past string -- try to find one that's not null
                for (int iter = 0; iter < 10 && s == null; iter++)
                {
                    s = UnicodeStrings[Random().Next(i)];
                }
                if (s == null)
                {
                    s = TestUtil.RandomUnicodeString(Random());
                }
            }
            else
            {
                s = TestUtil.RandomUnicodeString(Random());
            }
            return s;
        }

        [Test]
        public virtual void TestDocsWithField()
        {
            FieldCache cache = FieldCache_Fields.DEFAULT;
            cache.PurgeAllCaches();
            Assert.AreEqual(0, cache.CacheEntries.Length);
            cache.GetDoubles(Reader, "theDouble", true);

            // The double[] takes two slots (one w/ null parser, one
            // w/ real parser), and docsWithField should also
            // have been populated:
            Assert.AreEqual(3, cache.CacheEntries.Length);
            Bits bits = cache.GetDocsWithField(Reader, "theDouble");

            // No new entries should appear:
            Assert.AreEqual(3, cache.CacheEntries.Length);
            Assert.IsTrue(bits is Bits_MatchAllBits);

            Ints ints = cache.GetInts(Reader, "sparse", true);
            Assert.AreEqual(6, cache.CacheEntries.Length);
            Bits docsWithField = cache.GetDocsWithField(Reader, "sparse");
            Assert.AreEqual(6, cache.CacheEntries.Length);
            for (int i = 0; i < docsWithField.Length(); i++)
            {
                if (i % 2 == 0)
                {
                    Assert.IsTrue(docsWithField.Get(i));
                    Assert.AreEqual(i, ints.Get(i));
                }
                else
                {
                    Assert.IsFalse(docsWithField.Get(i));
                }
            }

            Ints numInts = cache.GetInts(Reader, "numInt", Random().NextBoolean());
            docsWithField = cache.GetDocsWithField(Reader, "numInt");
            for (int i = 0; i < docsWithField.Length(); i++)
            {
                if (i % 2 == 0)
                {
                    Assert.IsTrue(docsWithField.Get(i));
                    Assert.AreEqual(i, numInts.Get(i));
                }
                else
                {
                    Assert.IsFalse(docsWithField.Get(i));
                }
            }
        }

        //LUCENE TODO: Compilation Problems
        /*[Test]
        public virtual void TestGetDocsWithFieldThreadSafety()
        {
            FieldCache cache = FieldCache_Fields.DEFAULT;
            cache.PurgeAllCaches();

            int NUM_THREADS = 3;
            ThreadClass[] threads = new ThreadClass[NUM_THREADS];
            AtomicBoolean failed = new AtomicBoolean();
            AtomicInteger iters = new AtomicInteger();
            int NUM_ITER = 200 * RANDOM_MULTIPLIER;
            CyclicBarrier restart = new CyclicBarrier(NUM_THREADS, new RunnableAnonymousInnerClassHelper(this, cache, iters));
            for (int threadIDX = 0; threadIDX < NUM_THREADS; threadIDX++)
            {
                threads[threadIDX] = new ThreadAnonymousInnerClassHelper(this, cache, failed, iters, NUM_ITER, restart);
                threads[threadIDX].Start();
            }

            for (int threadIDX = 0; threadIDX < NUM_THREADS; threadIDX++)
            {
                threads[threadIDX].Join();
            }
            Assert.IsFalse(failed.Get());
        }

        private class RunnableAnonymousInnerClassHelper : IThreadRunnable
        {
            private readonly TestFieldCache OuterInstance;

            private FieldCache Cache;
            private AtomicInteger Iters;

            public RunnableAnonymousInnerClassHelper(TestFieldCache outerInstance, FieldCache cache, AtomicInteger iters)
            {
                this.OuterInstance = outerInstance;
                this.Cache = cache;
                this.Iters = iters;
            }

            public void Run()
            {
                Cache.PurgeAllCaches();
                Iters.IncrementAndGet();
            }
        }

        private class ThreadAnonymousInnerClassHelper : ThreadClass
        {
            private readonly TestFieldCache OuterInstance;

            private FieldCache Cache;
            private AtomicBoolean Failed;
            private AtomicInteger Iters;
            private int NUM_ITER;
            private CyclicBarrier Restart;

            public ThreadAnonymousInnerClassHelper(TestFieldCache outerInstance, FieldCache cache, AtomicBoolean failed, AtomicInteger iters, int NUM_ITER, CyclicBarrier restart)
            {
                this.OuterInstance = outerInstance;
                this.Cache = cache;
                this.Failed = failed;
                this.Iters = iters;
                this.NUM_ITER = NUM_ITER;
                this.Restart = restart;
            }

            public override void Run()
            {

                try
                {
                    while (!Failed.Get())
                    {
                        int op = Random().Next(3);
                        if (op == 0)
                        {
                            // Purge all caches & resume, once all
                            // threads get here:
                            Restart.@await();
                            if (Iters.Get() >= NUM_ITER)
                            {
                                break;
                            }
                        }
                        else if (op == 1)
                        {
                            Bits docsWithField = Cache.GetDocsWithField(Reader, "sparse");
                            for (int i = 0; i < docsWithField.Length(); i++)
                            {
                                Assert.AreEqual(i % 2 == 0, docsWithField.Get(i));
                            }
                        }
                        else
                        {
                            Ints ints = Cache.GetInts(Reader, "sparse", true);
                            Bits docsWithField = Cache.GetDocsWithField(Reader, "sparse");
                            for (int i = 0; i < docsWithField.Length(); i++)
                            {
                                if (i % 2 == 0)
                                {
                                    Assert.IsTrue(docsWithField.Get(i));
                                    Assert.AreEqual(i, ints.Get(i));
                                }
                                else
                                {
                                    Assert.IsFalse(docsWithField.Get(i));
                                }
                            }
                        }
                    }
                }
                catch (Exception t)
                {
                    Failed.Set(true);
                    Restart.Reset();
                    throw new Exception(t.Message, t);
                }
            }
        }*/

        [Test]
        public virtual void TestDocValuesIntegration()
        {
            AssumeTrue("3.x does not support docvalues", DefaultCodecSupportsDocValues());
            Directory dir = NewDirectory();
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, null);
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);
            Document doc = new Document();
            doc.Add(new BinaryDocValuesField("binary", new BytesRef("binary value")));
            doc.Add(new SortedDocValuesField("sorted", new BytesRef("sorted value")));
            doc.Add(new NumericDocValuesField("numeric", 42));
            if (DefaultCodecSupportsSortedSet())
            {
                doc.Add(new SortedSetDocValuesField("sortedset", new BytesRef("sortedset value1")));
                doc.Add(new SortedSetDocValuesField("sortedset", new BytesRef("sortedset value2")));
            }
            iw.AddDocument(doc);
            DirectoryReader ir = iw.Reader;
            iw.Dispose();
            AtomicReader ar = GetOnlySegmentReader(ir);

            BytesRef scratch = new BytesRef();

            // Binary type: can be retrieved via getTerms()
            try
            {
                FieldCache_Fields.DEFAULT.GetInts(ar, "binary", false);
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            BinaryDocValues binary = FieldCache_Fields.DEFAULT.GetTerms(ar, "binary", true);
            binary.Get(0, scratch);
            Assert.AreEqual("binary value", scratch.Utf8ToString());

            try
            {
                FieldCache_Fields.DEFAULT.GetTermsIndex(ar, "binary");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                FieldCache_Fields.DEFAULT.GetDocTermOrds(ar, "binary");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                new DocTermOrds(ar, null, "binary");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            Bits bits = FieldCache_Fields.DEFAULT.GetDocsWithField(ar, "binary");
            Assert.IsTrue(bits.Get(0));

            // Sorted type: can be retrieved via getTerms(), getTermsIndex(), getDocTermOrds()
            try
            {
                FieldCache_Fields.DEFAULT.GetInts(ar, "sorted", false);
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                new DocTermOrds(ar, null, "sorted");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            binary = FieldCache_Fields.DEFAULT.GetTerms(ar, "sorted", true);
            binary.Get(0, scratch);
            Assert.AreEqual("sorted value", scratch.Utf8ToString());

            SortedDocValues sorted = FieldCache_Fields.DEFAULT.GetTermsIndex(ar, "sorted");
            Assert.AreEqual(0, sorted.GetOrd(0));
            Assert.AreEqual(1, sorted.ValueCount);
            sorted.Get(0, scratch);
            Assert.AreEqual("sorted value", scratch.Utf8ToString());

            SortedSetDocValues sortedSet = FieldCache_Fields.DEFAULT.GetDocTermOrds(ar, "sorted");
            sortedSet.Document = 0;
            Assert.AreEqual(0, sortedSet.NextOrd());
            Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, sortedSet.NextOrd());
            Assert.AreEqual(1, sortedSet.ValueCount);

            bits = FieldCache_Fields.DEFAULT.GetDocsWithField(ar, "sorted");
            Assert.IsTrue(bits.Get(0));

            // Numeric type: can be retrieved via getInts() and so on
            Ints numeric = FieldCache_Fields.DEFAULT.GetInts(ar, "numeric", false);
            Assert.AreEqual(42, numeric.Get(0));

            try
            {
                FieldCache_Fields.DEFAULT.GetTerms(ar, "numeric", true);
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                FieldCache_Fields.DEFAULT.GetTermsIndex(ar, "numeric");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                FieldCache_Fields.DEFAULT.GetDocTermOrds(ar, "numeric");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            try
            {
                new DocTermOrds(ar, null, "numeric");
                Assert.Fail();
            }
            catch (InvalidOperationException expected)
            {
            }

            bits = FieldCache_Fields.DEFAULT.GetDocsWithField(ar, "numeric");
            Assert.IsTrue(bits.Get(0));

            // SortedSet type: can be retrieved via getDocTermOrds() 
            if (DefaultCodecSupportsSortedSet())
            {
                try
                {
                    FieldCache_Fields.DEFAULT.GetInts(ar, "sortedset", false);
                    Assert.Fail();
                }
                catch (InvalidOperationException expected)
                {
                }

                try
                {
                    FieldCache_Fields.DEFAULT.GetTerms(ar, "sortedset", true);
                    Assert.Fail();
                }
                catch (InvalidOperationException expected)
                {
                }

                try
                {
                    FieldCache_Fields.DEFAULT.GetTermsIndex(ar, "sortedset");
                    Assert.Fail();
                }
                catch (InvalidOperationException expected)
                {
                }

                try
                {
                    new DocTermOrds(ar, null, "sortedset");
                    Assert.Fail();
                }
                catch (InvalidOperationException expected)
                {
                }

                sortedSet = FieldCache_Fields.DEFAULT.GetDocTermOrds(ar, "sortedset");
                sortedSet.Document = 0;
                Assert.AreEqual(0, sortedSet.NextOrd());
                Assert.AreEqual(1, sortedSet.NextOrd());
                Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, sortedSet.NextOrd());
                Assert.AreEqual(2, sortedSet.ValueCount);

                bits = FieldCache_Fields.DEFAULT.GetDocsWithField(ar, "sortedset");
                Assert.IsTrue(bits.Get(0));
            }

            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public virtual void TestNonexistantFields()
        {
            Directory dir = NewDirectory();
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir);
            Document doc = new Document();
            iw.AddDocument(doc);
            DirectoryReader ir = iw.Reader;
            iw.Dispose();

            AtomicReader ar = GetOnlySegmentReader(ir);

            FieldCache cache = FieldCache_Fields.DEFAULT;
            cache.PurgeAllCaches();
            Assert.AreEqual(0, cache.CacheEntries.Length);

            Bytes bytes = cache.GetBytes(ar, "bogusbytes", true);
            Assert.AreEqual(0, bytes.Get(0));

            Shorts shorts = cache.GetShorts(ar, "bogusshorts", true);
            Assert.AreEqual(0, shorts.Get(0));

            Ints ints = cache.GetInts(ar, "bogusints", true);
            Assert.AreEqual(0, ints.Get(0));

            Longs longs = cache.GetLongs(ar, "boguslongs", true);
            Assert.AreEqual(0, longs.Get(0));

            Floats floats = cache.GetFloats(ar, "bogusfloats", true);
            Assert.AreEqual(0, floats.Get(0), 0.0f);

            Doubles doubles = cache.GetDoubles(ar, "bogusdoubles", true);
            Assert.AreEqual(0, doubles.Get(0), 0.0D);

            BytesRef scratch = new BytesRef();
            BinaryDocValues binaries = cache.GetTerms(ar, "bogusterms", true);
            binaries.Get(0, scratch);
            Assert.AreEqual(0, scratch.Length);

            SortedDocValues sorted = cache.GetTermsIndex(ar, "bogustermsindex");
            Assert.AreEqual(-1, sorted.GetOrd(0));
            sorted.Get(0, scratch);
            Assert.AreEqual(0, scratch.Length);

            SortedSetDocValues sortedSet = cache.GetDocTermOrds(ar, "bogusmultivalued");
            sortedSet.Document = 0;
            Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, sortedSet.NextOrd());

            Bits bits = cache.GetDocsWithField(ar, "bogusbits");
            Assert.IsFalse(bits.Get(0));

            // check that we cached nothing
            Assert.AreEqual(0, cache.CacheEntries.Length);
            ir.Dispose();
            dir.Dispose();
        }

        [Test]
        public virtual void TestNonIndexedFields()
        {
            Directory dir = NewDirectory();
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir);
            Document doc = new Document();
            doc.Add(new StoredField("bogusbytes", "bogus"));
            doc.Add(new StoredField("bogusshorts", "bogus"));
            doc.Add(new StoredField("bogusints", "bogus"));
            doc.Add(new StoredField("boguslongs", "bogus"));
            doc.Add(new StoredField("bogusfloats", "bogus"));
            doc.Add(new StoredField("bogusdoubles", "bogus"));
            doc.Add(new StoredField("bogusterms", "bogus"));
            doc.Add(new StoredField("bogustermsindex", "bogus"));
            doc.Add(new StoredField("bogusmultivalued", "bogus"));
            doc.Add(new StoredField("bogusbits", "bogus"));
            iw.AddDocument(doc);
            DirectoryReader ir = iw.Reader;
            iw.Dispose();

            AtomicReader ar = GetOnlySegmentReader(ir);

            FieldCache cache = FieldCache_Fields.DEFAULT;
            cache.PurgeAllCaches();
            Assert.AreEqual(0, cache.CacheEntries.Length);

            Bytes bytes = cache.GetBytes(ar, "bogusbytes", true);
            Assert.AreEqual(0, bytes.Get(0));

            Shorts shorts = cache.GetShorts(ar, "bogusshorts", true);
            Assert.AreEqual(0, shorts.Get(0));

            Ints ints = cache.GetInts(ar, "bogusints", true);
            Assert.AreEqual(0, ints.Get(0));

            Longs longs = cache.GetLongs(ar, "boguslongs", true);
            Assert.AreEqual(0, longs.Get(0));

            Floats floats = cache.GetFloats(ar, "bogusfloats", true);
            Assert.AreEqual(0, floats.Get(0), 0.0f);

            Doubles doubles = cache.GetDoubles(ar, "bogusdoubles", true);
            Assert.AreEqual(0, doubles.Get(0), 0.0D);

            BytesRef scratch = new BytesRef();
            BinaryDocValues binaries = cache.GetTerms(ar, "bogusterms", true);
            binaries.Get(0, scratch);
            Assert.AreEqual(0, scratch.Length);

            SortedDocValues sorted = cache.GetTermsIndex(ar, "bogustermsindex");
            Assert.AreEqual(-1, sorted.GetOrd(0));
            sorted.Get(0, scratch);
            Assert.AreEqual(0, scratch.Length);

            SortedSetDocValues sortedSet = cache.GetDocTermOrds(ar, "bogusmultivalued");
            sortedSet.Document = 0;
            Assert.AreEqual(SortedSetDocValues.NO_MORE_ORDS, sortedSet.NextOrd());

            Bits bits = cache.GetDocsWithField(ar, "bogusbits");
            Assert.IsFalse(bits.Get(0));

            // check that we cached nothing
            Assert.AreEqual(0, cache.CacheEntries.Length);
            ir.Dispose();
            dir.Dispose();
        }

        // Make sure that the use of GrowableWriter doesn't prevent from using the full long range
        [Test]
        public virtual void TestLongFieldCache()
        {
            Directory dir = NewDirectory();
            IndexWriterConfig cfg = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
            cfg.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, cfg);
            Document doc = new Document();
            LongField field = new LongField("f", 0L, Field.Store.YES);
            doc.Add(field);
            long[] values = new long[TestUtil.NextInt(Random(), 1, 10)];
            for (int i = 0; i < values.Length; ++i)
            {
                long v;
                switch (Random().Next(10))
                {
                    case 0:
                        v = long.MinValue;
                        break;
                    case 1:
                        v = 0;
                        break;
                    case 2:
                        v = long.MaxValue;
                        break;
                    default:
                        v = TestUtil.NextLong(Random(), -10, 10);
                        break;
                }
                values[i] = v;
                if (v == 0 && Random().NextBoolean())
                {
                    // missing
                    iw.AddDocument(new Document());
                }
                else
                {
                    field.LongValue = v;
                    iw.AddDocument(doc);
                }
            }
            iw.ForceMerge(1);
            DirectoryReader reader = iw.Reader;
            Longs longs = FieldCache_Fields.DEFAULT.GetLongs(GetOnlySegmentReader(reader), "f", false);
            for (int i = 0; i < values.Length; ++i)
            {
                Assert.AreEqual(values[i], longs.Get(i));
            }
            reader.Dispose();
            iw.Dispose();
            dir.Dispose();
        }

        // Make sure that the use of GrowableWriter doesn't prevent from using the full int range
        [Test]
        public virtual void TestIntFieldCache()
        {
            Directory dir = NewDirectory();
            IndexWriterConfig cfg = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
            cfg.SetMergePolicy(NewLogMergePolicy());
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, cfg);
            Document doc = new Document();
            IntField field = new IntField("f", 0, Field.Store.YES);
            doc.Add(field);
            int[] values = new int[TestUtil.NextInt(Random(), 1, 10)];
            for (int i = 0; i < values.Length; ++i)
            {
                int v;
                switch (Random().Next(10))
                {
                    case 0:
                        v = int.MinValue;
                        break;
                    case 1:
                        v = 0;
                        break;
                    case 2:
                        v = int.MaxValue;
                        break;
                    default:
                        v = TestUtil.NextInt(Random(), -10, 10);
                        break;
                }
                values[i] = v;
                if (v == 0 && Random().NextBoolean())
                {
                    // missing
                    iw.AddDocument(new Document());
                }
                else
                {
                    field.IntValue = v;
                    iw.AddDocument(doc);
                }
            }
            iw.ForceMerge(1);
            DirectoryReader reader = iw.Reader;
            Ints ints = FieldCache_Fields.DEFAULT.GetInts(GetOnlySegmentReader(reader), "f", false);
            for (int i = 0; i < values.Length; ++i)
            {
                Assert.AreEqual(values[i], ints.Get(i));
            }
            reader.Dispose();
            iw.Dispose();
            dir.Dispose();
        }

    }

}