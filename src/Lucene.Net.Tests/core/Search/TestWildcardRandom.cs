using System;
using System.Globalization;
using System.Text;

namespace Lucene.Net.Search
{
    using NUnit.Framework;
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using IndexReader = Lucene.Net.Index.IndexReader;
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
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using Term = Lucene.Net.Index.Term;
    using TestUtil = Lucene.Net.Util.TestUtil;

    /// <summary>
    /// Create an index with terms from 000-999.
    /// Generates random wildcards according to patterns,
    /// and validates the correct number of hits are returned.
    /// </summary>

    [TestFixture]
    public class TestWildcardRandom : LuceneTestCase
    {
        private IndexSearcher Searcher;
        private IndexReader Reader;
        private Directory Dir;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            Dir = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), Dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMaxBufferedDocs(TestUtil.NextInt(Random(), 50, 1000)));

            Document doc = new Document();
            Field field = NewStringField("field", "", Field.Store.NO);
            doc.Add(field);

            NumberFormatInfo df = new NumberFormatInfo();
            df.NumberDecimalDigits = 0;

            //NumberFormat df = new DecimalFormat("000", new DecimalFormatSymbols(Locale.ROOT));
            for (int i = 0; i < 1000; i++)
            {
                field.StringValue = i.ToString(df);
                writer.AddDocument(doc);
            }

            Reader = writer.Reader;
            Searcher = NewSearcher(Reader);
            writer.Dispose();
            if (VERBOSE)
            {
                Console.WriteLine("TEST: setUp searcher=" + Searcher);
            }
        }

        private char N()
        {
            return (char)(0x30 + Random().Next(10));
        }

        private string FillPattern(string wildcardPattern)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < wildcardPattern.Length; i++)
            {
                switch (wildcardPattern[i])
                {
                    case 'N':
                        sb.Append(N());
                        break;

                    default:
                        sb.Append(wildcardPattern[i]);
                        break;
                }
            }
            return sb.ToString();
        }

        private void AssertPatternHits(string pattern, int numHits)
        {
            // TODO: run with different rewrites
            string filledPattern = FillPattern(pattern);
            if (VERBOSE)
            {
                Console.WriteLine("TEST: run wildcard pattern=" + pattern + " filled=" + filledPattern);
            }
            Query wq = new WildcardQuery(new Term("field", filledPattern));
            TopDocs docs = Searcher.Search(wq, 25);
            Assert.AreEqual(numHits, docs.TotalHits, "Incorrect hits for pattern: " + pattern);
        }

        [TearDown]
        public override void TearDown()
        {
            Reader.Dispose();
            Dir.Dispose();
            base.TearDown();
        }

        [Test]
        public virtual void TestWildcards()
        {
            ;
            int num = AtLeast(1);
            for (int i = 0; i < num; i++)
            {
                AssertPatternHits("NNN", 1);
                AssertPatternHits("?NN", 10);
                AssertPatternHits("N?N", 10);
                AssertPatternHits("NN?", 10);
            }

            for (int i = 0; i < num; i++)
            {
                AssertPatternHits("??N", 100);
                AssertPatternHits("N??", 100);
                AssertPatternHits("???", 1000);

                AssertPatternHits("NN*", 10);
                AssertPatternHits("N*", 100);
                AssertPatternHits("*", 1000);

                AssertPatternHits("*NN", 10);
                AssertPatternHits("*N", 100);

                AssertPatternHits("N*N", 10);

                // combo of ? and * operators
                AssertPatternHits("?N*", 100);
                AssertPatternHits("N?*", 100);

                AssertPatternHits("*N?", 100);
                AssertPatternHits("*??", 1000);
                AssertPatternHits("*?N", 100);
            }
        }
    }
}