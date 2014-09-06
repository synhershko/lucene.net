namespace Lucene.Net.Search.Spans
{
    using NUnit.Framework;

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

    using Analyzer = Lucene.Net.Analysis.Analyzer;
    using CharacterRunAutomaton = Lucene.Net.Util.Automaton.CharacterRunAutomaton;
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using MockTokenizer = Lucene.Net.Analysis.MockTokenizer;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using RegExp = Lucene.Net.Util.Automaton.RegExp;
    using Term = Lucene.Net.Index.Term;

    [TestFixture]
    public class TestSpanFirstQuery : LuceneTestCase
    {
        [Test]
        public virtual void TestStartPositions()
        {
            Directory dir = NewDirectory();

            // mimic StopAnalyzer
            CharacterRunAutomaton stopSet = new CharacterRunAutomaton((new RegExp("the|a|of")).ToAutomaton());
            Analyzer analyzer = new MockAnalyzer(Random(), MockTokenizer.SIMPLE, true, stopSet);

            RandomIndexWriter writer = new RandomIndexWriter(Random(), dir, analyzer);
            Document doc = new Document();
            doc.Add(NewTextField("field", "the quick brown fox", Field.Store.NO));
            writer.AddDocument(doc);
            Document doc2 = new Document();
            doc2.Add(NewTextField("field", "quick brown fox", Field.Store.NO));
            writer.AddDocument(doc2);

            IndexReader reader = writer.Reader;
            IndexSearcher searcher = NewSearcher(reader);

            // user queries on "starts-with quick"
            SpanQuery sfq = new SpanFirstQuery(new SpanTermQuery(new Term("field", "quick")), 1);
            Assert.AreEqual(1, searcher.Search(sfq, 10).TotalHits);

            // user queries on "starts-with the quick"
            SpanQuery include = new SpanFirstQuery(new SpanTermQuery(new Term("field", "quick")), 2);
            sfq = new SpanNotQuery(include, sfq);
            Assert.AreEqual(1, searcher.Search(sfq, 10).TotalHits);

            writer.Dispose();
            reader.Dispose();
            dir.Dispose();
        }
    }
}