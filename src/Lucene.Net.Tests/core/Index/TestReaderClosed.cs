namespace Lucene.Net.Index
{
    using NUnit.Framework;
    using AlreadyClosedException = Lucene.Net.Store.AlreadyClosedException;
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using IndexSearcher = Lucene.Net.Search.IndexSearcher;
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
    using MockTokenizer = Lucene.Net.Analysis.MockTokenizer;
    using TermRangeQuery = Lucene.Net.Search.TermRangeQuery;
    using TestUtil = Lucene.Net.Util.TestUtil;

    [TestFixture]
    public class TestReaderClosed : LuceneTestCase
    {
        private IndexReader Reader;
        private Directory Dir;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            Dir = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), Dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random(), MockTokenizer.KEYWORD, false)).SetMaxBufferedDocs(TestUtil.NextInt(Random(), 50, 1000)));

            Document doc = new Document();
            Field field = NewStringField("field", "", Field.Store.NO);
            doc.Add(field);

            // we generate aweful prefixes: good for testing.
            // but for preflex codec, the test can be very slow, so use less iterations.
            int num = AtLeast(10);
            for (int i = 0; i < num; i++)
            {
                field.StringValue = TestUtil.RandomUnicodeString(Random(), 10);
                writer.AddDocument(doc);
            }
            Reader = writer.Reader;
            writer.Dispose();
        }

        [Test]
        public virtual void Test()
        {
            Assert.IsTrue(Reader.RefCount > 0);
            IndexSearcher searcher = NewSearcher(Reader);
            TermRangeQuery query = TermRangeQuery.NewStringRange("field", "a", "z", true, true);
            searcher.Search(query, 5);
            Reader.Dispose();
            try
            {
                searcher.Search(query, 5);
            }
            catch (AlreadyClosedException ace)
            {
                // expected
            }
        }

        // LUCENE-3800
        [Test]
        public virtual void TestReaderChaining()
        {
            Assert.IsTrue(Reader.RefCount > 0);
            IndexReader wrappedReader = SlowCompositeReaderWrapper.Wrap(Reader);
            wrappedReader = new ParallelAtomicReader((AtomicReader)wrappedReader);

            IndexSearcher searcher = NewSearcher(wrappedReader);
            TermRangeQuery query = TermRangeQuery.NewStringRange("field", "a", "z", true, true);
            searcher.Search(query, 5);
            Reader.Dispose(); // close original child reader
            try
            {
                searcher.Search(query, 5);
            }
            catch (AlreadyClosedException ace)
            {
                Assert.AreEqual("this IndexReader cannot be used anymore as one of its child readers was closed", ace.Message);
            }
            finally
            {
                // shutdown executor: in case of wrap-wrap-wrapping
                searcher.IndexReader.Dispose();
            }
        }

        [TearDown]
        public override void TearDown()
        {
            Dir.Dispose();
            base.TearDown();
        }
    }
}