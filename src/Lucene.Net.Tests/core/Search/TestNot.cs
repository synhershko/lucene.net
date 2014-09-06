namespace Lucene.Net.Search
{
    using NUnit.Framework;
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;

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

    using Field = Lucene.Net.Document.Field;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using Term = Lucene.Net.Index.Term;

    /// <summary>
    /// Similarity unit test.
    ///
    ///
    /// </summary>
    [TestFixture]
    public class TestNot : LuceneTestCase
    {
        [Test]
        public virtual void TestNot_Mem()
        {
            Directory store = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), store);

            Document d1 = new Document();
            d1.Add(NewTextField("field", "a b", Field.Store.YES));

            writer.AddDocument(d1);
            IndexReader reader = writer.Reader;

            IndexSearcher searcher = NewSearcher(reader);

            BooleanQuery query = new BooleanQuery();
            query.Add(new TermQuery(new Term("field", "a")), BooleanClause.Occur.SHOULD);
            query.Add(new TermQuery(new Term("field", "b")), BooleanClause.Occur.MUST_NOT);

            ScoreDoc[] hits = searcher.Search(query, null, 1000).ScoreDocs;
            Assert.AreEqual(0, hits.Length);
            writer.Dispose();
            reader.Dispose();
            store.Dispose();
        }
    }
}