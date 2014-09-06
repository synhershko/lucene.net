using Lucene.Net.Analysis.Tokenattributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lucene.Net.Search.Spans
{
    using NUnit.Framework;
    using System.IO;

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

    using Analyzer = Lucene.Net.Analysis.Analyzer;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using DefaultSimilarity = Lucene.Net.Search.Similarities.DefaultSimilarity;
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockTokenizer = Lucene.Net.Analysis.MockTokenizer;
    using PayloadHelper = Lucene.Net.Search.Payloads.PayloadHelper;
    using PayloadSpanUtil = Lucene.Net.Search.Payloads.PayloadSpanUtil;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;
    using Similarity = Lucene.Net.Search.Similarities.Similarity;
    using Term = Lucene.Net.Index.Term;
    using TextField = Lucene.Net.Document.TextField;
    using TokenFilter = Lucene.Net.Analysis.TokenFilter;
    using Tokenizer = Lucene.Net.Analysis.Tokenizer;
    using TokenStream = Lucene.Net.Analysis.TokenStream;

    [TestFixture]
    public class TestPayloadSpans : LuceneTestCase
    {
        private IndexSearcher Searcher_Renamed;
        private Similarity Similarity = new DefaultSimilarity();
        protected internal IndexReader IndexReader;
        private IndexReader CloseIndexReader;
        private Directory Directory;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            PayloadHelper helper = new PayloadHelper();
            Searcher_Renamed = helper.SetUp(Random(), Similarity, 1000);
            IndexReader = Searcher_Renamed.IndexReader;
        }

        [Test]
        public virtual void TestSpanTermQuery()
        {
            SpanTermQuery stq;
            Spans spans;
            stq = new SpanTermQuery(new Term(PayloadHelper.FIELD, "seventy"));
            spans = MultiSpansWrapper.Wrap(IndexReader.Context, stq);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 100, 1, 1, 1);

            stq = new SpanTermQuery(new Term(PayloadHelper.NO_PAYLOAD_FIELD, "seventy"));
            spans = MultiSpansWrapper.Wrap(IndexReader.Context, stq);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 100, 0, 0, 0);
        }

        [Test]
        public virtual void TestSpanFirst()
        {
            SpanQuery match;
            SpanFirstQuery sfq;
            match = new SpanTermQuery(new Term(PayloadHelper.FIELD, "one"));
            sfq = new SpanFirstQuery(match, 2);
            Spans spans = MultiSpansWrapper.Wrap(IndexReader.Context, sfq);
            CheckSpans(spans, 109, 1, 1, 1);
            //Test more complicated subclause
            SpanQuery[] clauses = new SpanQuery[2];
            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "one"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "hundred"));
            match = new SpanNearQuery(clauses, 0, true);
            sfq = new SpanFirstQuery(match, 2);
            CheckSpans(MultiSpansWrapper.Wrap(IndexReader.Context, sfq), 100, 2, 1, 1);

            match = new SpanNearQuery(clauses, 0, false);
            sfq = new SpanFirstQuery(match, 2);
            CheckSpans(MultiSpansWrapper.Wrap(IndexReader.Context, sfq), 100, 2, 1, 1);
        }

        [Test]
        public virtual void TestSpanNot()
        {
            SpanQuery[] clauses = new SpanQuery[2];
            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "one"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "three"));
            SpanQuery spq = new SpanNearQuery(clauses, 5, true);
            SpanNotQuery snq = new SpanNotQuery(spq, new SpanTermQuery(new Term(PayloadHelper.FIELD, "two")));

            Directory directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new PayloadAnalyzer(this)).SetSimilarity(Similarity));

            Document doc = new Document();
            doc.Add(NewTextField(PayloadHelper.FIELD, "one two three one four three", Field.Store.YES));
            writer.AddDocument(doc);
            IndexReader reader = writer.Reader;
            writer.Dispose();

            CheckSpans(MultiSpansWrapper.Wrap(reader.Context, snq), 1, new int[] { 2 });
            reader.Dispose();
            directory.Dispose();
        }

        [Test]
        public virtual void TestNestedSpans()
        {
            SpanTermQuery stq;
            Spans spans;
            IndexSearcher searcher = Searcher;
            stq = new SpanTermQuery(new Term(PayloadHelper.FIELD, "mark"));
            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, stq);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 0, null);

            SpanQuery[] clauses = new SpanQuery[3];
            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "rr"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "yy"));
            clauses[2] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "xx"));
            SpanNearQuery spanNearQuery = new SpanNearQuery(clauses, 12, false);

            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, spanNearQuery);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 2, new int[] { 3, 3 });

            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "xx"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "rr"));
            clauses[2] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "yy"));

            spanNearQuery = new SpanNearQuery(clauses, 6, true);

            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, spanNearQuery);

            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 1, new int[] { 3 });

            clauses = new SpanQuery[2];

            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "xx"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "rr"));

            spanNearQuery = new SpanNearQuery(clauses, 6, true);

            // xx within 6 of rr

            SpanQuery[] clauses2 = new SpanQuery[2];

            clauses2[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "yy"));
            clauses2[1] = spanNearQuery;

            SpanNearQuery nestedSpanNearQuery = new SpanNearQuery(clauses2, 6, false);

            // yy within 6 of xx within 6 of rr

            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, nestedSpanNearQuery);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 2, new int[] { 3, 3 });
            CloseIndexReader.Dispose();
            Directory.Dispose();
        }

        [Test]
        public virtual void TestFirstClauseWithoutPayload()
        {
            Spans spans;
            IndexSearcher searcher = Searcher;

            SpanQuery[] clauses = new SpanQuery[3];
            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "nopayload"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "qq"));
            clauses[2] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "ss"));

            SpanNearQuery spanNearQuery = new SpanNearQuery(clauses, 6, true);

            SpanQuery[] clauses2 = new SpanQuery[2];

            clauses2[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "pp"));
            clauses2[1] = spanNearQuery;

            SpanNearQuery snq = new SpanNearQuery(clauses2, 6, false);

            SpanQuery[] clauses3 = new SpanQuery[2];

            clauses3[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "np"));
            clauses3[1] = snq;

            SpanNearQuery nestedSpanNearQuery = new SpanNearQuery(clauses3, 6, false);
            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, nestedSpanNearQuery);

            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 1, new int[] { 3 });
            CloseIndexReader.Dispose();
            Directory.Dispose();
        }

        [Test]
        public virtual void TestHeavilyNestedSpanQuery()
        {
            Spans spans;
            IndexSearcher searcher = Searcher;

            SpanQuery[] clauses = new SpanQuery[3];
            clauses[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "one"));
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "two"));
            clauses[2] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "three"));

            SpanNearQuery spanNearQuery = new SpanNearQuery(clauses, 5, true);

            clauses = new SpanQuery[3];
            clauses[0] = spanNearQuery;
            clauses[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "five"));
            clauses[2] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "six"));

            SpanNearQuery spanNearQuery2 = new SpanNearQuery(clauses, 6, true);

            SpanQuery[] clauses2 = new SpanQuery[2];
            clauses2[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "eleven"));
            clauses2[1] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "ten"));
            SpanNearQuery spanNearQuery3 = new SpanNearQuery(clauses2, 2, false);

            SpanQuery[] clauses3 = new SpanQuery[3];
            clauses3[0] = new SpanTermQuery(new Term(PayloadHelper.FIELD, "nine"));
            clauses3[1] = spanNearQuery2;
            clauses3[2] = spanNearQuery3;

            SpanNearQuery nestedSpanNearQuery = new SpanNearQuery(clauses3, 6, false);

            spans = MultiSpansWrapper.Wrap(searcher.TopReaderContext, nestedSpanNearQuery);
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            CheckSpans(spans, 2, new int[] { 8, 8 });
            CloseIndexReader.Dispose();
            Directory.Dispose();
        }

        [Test]
        public virtual void TestShrinkToAfterShortestMatch()
        {
            Directory directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new TestPayloadAnalyzer(this)));

            Document doc = new Document();
            doc.Add(new TextField("content", new StringReader("a b c d e f g h i j a k")));
            writer.AddDocument(doc);

            IndexReader reader = writer.Reader;
            IndexSearcher @is = NewSearcher(reader);
            writer.Dispose();

            SpanTermQuery stq1 = new SpanTermQuery(new Term("content", "a"));
            SpanTermQuery stq2 = new SpanTermQuery(new Term("content", "k"));
            SpanQuery[] sqs = new SpanQuery[] { stq1, stq2 };
            SpanNearQuery snq = new SpanNearQuery(sqs, 1, true);
            Spans spans = MultiSpansWrapper.Wrap(@is.TopReaderContext, snq);

            TopDocs topDocs = @is.Search(snq, 1);
            HashSet<string> payloadSet = new HashSet<string>();
            for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
            {
                while (spans.Next())
                {
                    ICollection<sbyte[]> payloads = spans.Payload;

                    foreach (sbyte[] payload in payloads)
                    {
                        payloadSet.Add(Encoding.UTF8.GetString((byte[])(Array)payload));
                    }
                }
            }
            Assert.AreEqual(2, payloadSet.Count);
            Assert.IsTrue(payloadSet.Contains("a:Noise:10"));
            Assert.IsTrue(payloadSet.Contains("k:Noise:11"));
            reader.Dispose();
            directory.Dispose();
        }

        [Test]
        public virtual void TestShrinkToAfterShortestMatch2()
        {
            Directory directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new TestPayloadAnalyzer(this)));

            Document doc = new Document();
            doc.Add(new TextField("content", new StringReader("a b a d k f a h i k a k")));
            writer.AddDocument(doc);
            IndexReader reader = writer.Reader;
            IndexSearcher @is = NewSearcher(reader);
            writer.Dispose();

            SpanTermQuery stq1 = new SpanTermQuery(new Term("content", "a"));
            SpanTermQuery stq2 = new SpanTermQuery(new Term("content", "k"));
            SpanQuery[] sqs = new SpanQuery[] { stq1, stq2 };
            SpanNearQuery snq = new SpanNearQuery(sqs, 0, true);
            Spans spans = MultiSpansWrapper.Wrap(@is.TopReaderContext, snq);

            TopDocs topDocs = @is.Search(snq, 1);
            HashSet<string> payloadSet = new HashSet<string>();
            for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
            {
                while (spans.Next())
                {
                    ICollection<sbyte[]> payloads = spans.Payload;
                    foreach (sbyte[] payload in payloads)
                    {
                        payloadSet.Add(Encoding.UTF8.GetString((byte[])(Array)payload));
                    }
                }
            }
            Assert.AreEqual(2, payloadSet.Count);
            Assert.IsTrue(payloadSet.Contains("a:Noise:10"));
            Assert.IsTrue(payloadSet.Contains("k:Noise:11"));
            reader.Dispose();
            directory.Dispose();
        }

        [Test]
        public virtual void TestShrinkToAfterShortestMatch3()
        {
            Directory directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new TestPayloadAnalyzer(this)));

            Document doc = new Document();
            doc.Add(new TextField("content", new StringReader("j k a l f k k p a t a k l k t a")));
            writer.AddDocument(doc);
            IndexReader reader = writer.Reader;
            IndexSearcher @is = NewSearcher(reader);
            writer.Dispose();

            SpanTermQuery stq1 = new SpanTermQuery(new Term("content", "a"));
            SpanTermQuery stq2 = new SpanTermQuery(new Term("content", "k"));
            SpanQuery[] sqs = new SpanQuery[] { stq1, stq2 };
            SpanNearQuery snq = new SpanNearQuery(sqs, 0, true);
            Spans spans = MultiSpansWrapper.Wrap(@is.TopReaderContext, snq);

            TopDocs topDocs = @is.Search(snq, 1);
            HashSet<string> payloadSet = new HashSet<string>();
            for (int i = 0; i < topDocs.ScoreDocs.Length; i++)
            {
                while (spans.Next())
                {
                    ICollection<sbyte[]> payloads = spans.Payload;

                    foreach (sbyte[] payload in payloads)
                    {
                        payloadSet.Add(Encoding.UTF8.GetString((byte[])(Array)payload));
                    }
                }
            }
            Assert.AreEqual(2, payloadSet.Count);
            if (VERBOSE)
            {
                foreach (String payload in payloadSet)
                {
                    Console.WriteLine("match:" + payload);
                }
            }
            Assert.IsTrue(payloadSet.Contains("a:Noise:10"));
            Assert.IsTrue(payloadSet.Contains("k:Noise:11"));
            reader.Dispose();
            directory.Dispose();
        }

        [Test]
        public virtual void TestPayloadSpanUtil()
        {
            Directory directory = NewDirectory();
            RandomIndexWriter writer = new RandomIndexWriter(Random(), directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new PayloadAnalyzer(this)).SetSimilarity(Similarity));

            Document doc = new Document();
            doc.Add(NewTextField(PayloadHelper.FIELD, "xx rr yy mm  pp", Field.Store.YES));
            writer.AddDocument(doc);

            IndexReader reader = writer.Reader;
            writer.Dispose();
            IndexSearcher searcher = NewSearcher(reader);

            PayloadSpanUtil psu = new PayloadSpanUtil(searcher.TopReaderContext);

            ICollection<sbyte[]> payloads = psu.GetPayloadsForQuery(new TermQuery(new Term(PayloadHelper.FIELD, "rr")));
            if (VERBOSE)
            {
                Console.WriteLine("Num payloads:" + payloads.Count);
                foreach (sbyte[] bytes in payloads)
                {
                    Console.WriteLine(Encoding.UTF8.GetString((byte[])(Array)bytes));
                }
            }
            reader.Dispose();
            directory.Dispose();
        }

        private void CheckSpans(Spans spans, int expectedNumSpans, int expectedNumPayloads, int expectedPayloadLength, int expectedFirstByte)
        {
            Assert.IsTrue(spans != null, "spans is null and it shouldn't be");
            //each position match should have a span associated with it, since there is just one underlying term query, there should
            //only be one entry in the span
            int seen = 0;
            while (spans.Next() == true)
            {
                //if we expect payloads, then isPayloadAvailable should be true
                if (expectedNumPayloads > 0)
                {
                    Assert.IsTrue(spans.PayloadAvailable == true, "isPayloadAvailable is not returning the correct value: " + spans.PayloadAvailable + " and it should be: " + (expectedNumPayloads > 0));
                }
                else
                {
                    Assert.IsTrue(spans.PayloadAvailable == false, "isPayloadAvailable should be false");
                }
                //See payload helper, for the PayloadHelper.FIELD field, there is a single byte payload at every token
                if (spans.PayloadAvailable)
                {
                    ICollection<sbyte[]> payload = spans.Payload;
                    Assert.IsTrue(payload.Count == expectedNumPayloads, "payload Size: " + payload.Count + " is not: " + expectedNumPayloads);
                    foreach (sbyte[] thePayload in payload)
                    {
                        Assert.IsTrue(thePayload.Length == expectedPayloadLength, "payload[0] Size: " + thePayload.Length + " is not: " + expectedPayloadLength);
                        Assert.IsTrue(thePayload[0] == expectedFirstByte, thePayload[0] + " does not equal: " + expectedFirstByte);
                    }
                }
                seen++;
            }
            Assert.IsTrue(seen == expectedNumSpans, seen + " does not equal: " + expectedNumSpans);
        }

        private IndexSearcher Searcher
        {
            get
            {
                Directory = NewDirectory();
                string[] docs = new string[] { "xx rr yy mm  pp", "xx yy mm rr pp", "nopayload qq ss pp np", "one two three four five six seven eight nine ten eleven", "nine one two three four five six seven eight eleven ten" };
                RandomIndexWriter writer = new RandomIndexWriter(Random(), Directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new PayloadAnalyzer(this)).SetSimilarity(Similarity));

                Document doc = null;
                for (int i = 0; i < docs.Length; i++)
                {
                    doc = new Document();
                    string docText = docs[i];
                    doc.Add(NewTextField(PayloadHelper.FIELD, docText, Field.Store.YES));
                    writer.AddDocument(doc);
                }

                CloseIndexReader = writer.Reader;
                writer.Dispose();

                IndexSearcher searcher = NewSearcher(CloseIndexReader);
                return searcher;
            }
        }

        private void CheckSpans(Spans spans, int numSpans, int[] numPayloads)
        {
            int cnt = 0;

            while (spans.Next() == true)
            {
                if (VERBOSE)
                {
                    Console.WriteLine("\nSpans Dump --");
                }
                if (spans.PayloadAvailable)
                {
                    ICollection<sbyte[]> payload = spans.Payload;
                    if (VERBOSE)
                    {
                        Console.WriteLine("payloads for span:" + payload.Count);
                        foreach (sbyte[] bytes in payload)
                        {
                            Console.WriteLine("doc:" + spans.Doc() + " s:" + spans.Start() + " e:" + spans.End() + " " + Encoding.UTF8.GetString((byte[])(Array)bytes));
                        }
                    }

                    Assert.AreEqual(numPayloads[cnt], payload.Count);
                }
                else
                {
                    Assert.IsFalse(numPayloads.Length > 0 && numPayloads[cnt] > 0, "Expected spans:" + numPayloads[cnt] + " found: 0");
                }
                cnt++;
            }

            Assert.AreEqual(numSpans, cnt);
        }

        internal sealed class PayloadAnalyzer : Analyzer
        {
            private readonly TestPayloadSpans OuterInstance;

            public PayloadAnalyzer(TestPayloadSpans outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
            {
                Tokenizer result = new MockTokenizer(reader, MockTokenizer.SIMPLE, true);
                return new TokenStreamComponents(result, new PayloadFilter(OuterInstance, result));
            }
        }

        internal sealed class PayloadFilter : TokenFilter
        {
            private readonly TestPayloadSpans OuterInstance;

            internal HashSet<string> Entities = new HashSet<string>();
            internal HashSet<string> Nopayload = new HashSet<string>();
            internal int Pos;
            internal IPayloadAttribute PayloadAtt;
            internal ICharTermAttribute TermAtt;
            internal IPositionIncrementAttribute PosIncrAtt;

            public PayloadFilter(TestPayloadSpans outerInstance, TokenStream input)
                : base(input)
            {
                this.OuterInstance = outerInstance;
                Pos = 0;
                Entities.Add("xx");
                Entities.Add("one");
                Nopayload.Add("nopayload");
                Nopayload.Add("np");
                TermAtt = AddAttribute<ICharTermAttribute>();
                PosIncrAtt = AddAttribute<IPositionIncrementAttribute>();
                PayloadAtt = AddAttribute<IPayloadAttribute>();
            }

            public override bool IncrementToken()
            {
                if (Input.IncrementToken())
                {
                    string token = TermAtt.ToString();

                    if (!Nopayload.Contains(token))
                    {
                        if (Entities.Contains(token))
                        {
                            PayloadAtt.Payload = new BytesRef(token + ":Entity:" + Pos);
                        }
                        else
                        {
                            PayloadAtt.Payload = new BytesRef(token + ":Noise:" + Pos);
                        }
                    }
                    Pos += PosIncrAtt.PositionIncrement;
                    return true;
                }
                return false;
            }

            public override void Reset()
            {
                base.Reset();
                this.Pos = 0;
            }
        }

        public sealed class TestPayloadAnalyzer : Analyzer
        {
            private readonly TestPayloadSpans OuterInstance;

            public TestPayloadAnalyzer(TestPayloadSpans outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
            {
                Tokenizer result = new MockTokenizer(reader, MockTokenizer.SIMPLE, true);
                return new TokenStreamComponents(result, new PayloadFilter(OuterInstance, result));
            }
        }
    }
}