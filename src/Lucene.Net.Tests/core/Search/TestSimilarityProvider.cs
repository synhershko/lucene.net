namespace Lucene.Net.Search
{
    using NUnit.Framework;
    using AtomicReader = Lucene.Net.Index.AtomicReader;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Directory = Lucene.Net.Store.Directory;
    using DirectoryReader = Lucene.Net.Index.DirectoryReader;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FieldInvertState = Lucene.Net.Index.FieldInvertState;
    using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

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

    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using NumericDocValues = Lucene.Net.Index.NumericDocValues;
    using PerFieldSimilarityWrapper = Lucene.Net.Search.Similarities.PerFieldSimilarityWrapper;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using Similarity = Lucene.Net.Search.Similarities.Similarity;
    using SlowCompositeReaderWrapper = Lucene.Net.Index.SlowCompositeReaderWrapper;
    using Term = Lucene.Net.Index.Term;
    using TFIDFSimilarity = Lucene.Net.Search.Similarities.TFIDFSimilarity;

    [TestFixture]
    public class TestSimilarityProvider : LuceneTestCase
    {
        private Directory Directory;
        private DirectoryReader Reader;
        private IndexSearcher Searcher;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            Directory = NewDirectory();
            PerFieldSimilarityWrapper sim = new ExampleSimilarityProvider(this);
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetSimilarity(sim);
            RandomIndexWriter iw = new RandomIndexWriter(Random(), Directory, iwc);
            Document doc = new Document();
            Field field = NewTextField("foo", "", Field.Store.NO);
            doc.Add(field);
            Field field2 = NewTextField("bar", "", Field.Store.NO);
            doc.Add(field2);

            field.StringValue = "quick brown fox";
            field2.StringValue = "quick brown fox";
            iw.AddDocument(doc);
            field.StringValue = "jumps over lazy brown dog";
            field2.StringValue = "jumps over lazy brown dog";
            iw.AddDocument(doc);
            Reader = iw.Reader;
            iw.Dispose();
            Searcher = NewSearcher(Reader);
            Searcher.Similarity = sim;
        }

        [TearDown]
        public override void TearDown()
        {
            Reader.Dispose();
            Directory.Dispose();
            base.TearDown();
        }

        [Test]
        public virtual void TestBasics()
        {
            // sanity check of norms writer
            // TODO: generalize
            AtomicReader slow = SlowCompositeReaderWrapper.Wrap(Reader);
            NumericDocValues fooNorms = slow.GetNormValues("foo");
            NumericDocValues barNorms = slow.GetNormValues("bar");
            for (int i = 0; i < slow.MaxDoc(); i++)
            {
                Assert.IsFalse(fooNorms.Get(i) == barNorms.Get(i));
            }

            // sanity check of searching
            TopDocs foodocs = Searcher.Search(new TermQuery(new Term("foo", "brown")), 10);
            Assert.IsTrue(foodocs.TotalHits > 0);
            TopDocs bardocs = Searcher.Search(new TermQuery(new Term("bar", "brown")), 10);
            Assert.IsTrue(bardocs.TotalHits > 0);
            Assert.IsTrue(foodocs.ScoreDocs[0].Score < bardocs.ScoreDocs[0].Score);
        }

        private class ExampleSimilarityProvider : PerFieldSimilarityWrapper
        {
            private readonly TestSimilarityProvider OuterInstance;

            public ExampleSimilarityProvider(TestSimilarityProvider outerInstance)
            {
                this.OuterInstance = outerInstance;
                Sim1 = new Sim1(OuterInstance);
                Sim2 = new Sim2(OuterInstance);
            }

            private Similarity Sim1;
            private Similarity Sim2;

            public override Similarity Get(string field)
            {
                if (field.Equals("foo"))
                {
                    return Sim1;
                }
                else
                {
                    return Sim2;
                }
            }
        }

        private class Sim1 : TFIDFSimilarity
        {
            private readonly TestSimilarityProvider OuterInstance;

            public Sim1(TestSimilarityProvider outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public override long EncodeNormValue(float f)
            {
                return (long)f;
            }

            public override float DecodeNormValue(long norm)
            {
                return norm;
            }

            public override float Coord(int overlap, int maxOverlap)
            {
                return 1f;
            }

            public override float QueryNorm(float sumOfSquaredWeights)
            {
                return 1f;
            }

            public override float LengthNorm(FieldInvertState state)
            {
                return 1f;
            }

            public override float SloppyFreq(int distance)
            {
                return 1f;
            }

            public override float Tf(float freq)
            {
                return 1f;
            }

            public override float Idf(long docFreq, long numDocs)
            {
                return 1f;
            }

            public override float ScorePayload(int doc, int start, int end, BytesRef payload)
            {
                return 1f;
            }
        }

        private class Sim2 : TFIDFSimilarity
        {
            private readonly TestSimilarityProvider OuterInstance;

            public Sim2(TestSimilarityProvider outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public override long EncodeNormValue(float f)
            {
                return (long)f;
            }

            public override float DecodeNormValue(long norm)
            {
                return norm;
            }

            public override float Coord(int overlap, int maxOverlap)
            {
                return 1f;
            }

            public override float QueryNorm(float sumOfSquaredWeights)
            {
                return 1f;
            }

            public override float LengthNorm(FieldInvertState state)
            {
                return 10f;
            }

            public override float SloppyFreq(int distance)
            {
                return 10f;
            }

            public override float Tf(float freq)
            {
                return 10f;
            }

            public override float Idf(long docFreq, long numDocs)
            {
                return 10f;
            }

            public override float ScorePayload(int doc, int start, int end, BytesRef payload)
            {
                return 1f;
            }
        }
    }
}