using System;
using System.Collections.Generic;

namespace Lucene.Net.Search
{
    using Lucene.Net.Index;
    using Lucene.Net.Store;
    using NUnit.Framework;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using DefaultSimilarity = Lucene.Net.Search.Similarities.DefaultSimilarity;
    using Document = Lucene.Net.Document.Document;
    using Entry = Lucene.Net.Search.FieldValueHitQueue.Entry;
    using Field = Lucene.Net.Document.Field;
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

    [TestFixture]
    public class TestElevationComparator : LuceneTestCase
    {
        private readonly IDictionary<BytesRef, int?> Priority = new Dictionary<BytesRef, int?>();

        [Test]
        public virtual void TestSorting()
        {
            Directory directory = NewDirectory();
            IndexWriter writer = new IndexWriter(directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMaxBufferedDocs(2).SetMergePolicy(NewLogMergePolicy(1000)).SetSimilarity(new DefaultSimilarity()));
            writer.AddDocument(Adoc(new string[] { "id", "a", "title", "ipod", "str_s", "a" }));
            writer.AddDocument(Adoc(new string[] { "id", "b", "title", "ipod ipod", "str_s", "b" }));
            writer.AddDocument(Adoc(new string[] { "id", "c", "title", "ipod ipod ipod", "str_s", "c" }));
            writer.AddDocument(Adoc(new string[] { "id", "x", "title", "boosted", "str_s", "x" }));
            writer.AddDocument(Adoc(new string[] { "id", "y", "title", "boosted boosted", "str_s", "y" }));
            writer.AddDocument(Adoc(new string[] { "id", "z", "title", "boosted boosted boosted", "str_s", "z" }));

            IndexReader r = DirectoryReader.Open(writer, true);
            writer.Dispose();

            IndexSearcher searcher = NewSearcher(r);
            searcher.Similarity = new DefaultSimilarity();

            RunTest(searcher, true);
            RunTest(searcher, false);

            r.Dispose();
            directory.Dispose();
        }

        private void RunTest(IndexSearcher searcher, bool reversed)
        {
            BooleanQuery newq = new BooleanQuery(false);
            TermQuery query = new TermQuery(new Term("title", "ipod"));

            newq.Add(query, BooleanClause.Occur.SHOULD);
            newq.Add(GetElevatedQuery(new string[] { "id", "a", "id", "x" }), BooleanClause.Occur.SHOULD);

            Sort sort = new Sort(new SortField("id", new ElevationComparatorSource(Priority), false), new SortField(null, SortField.Type_e.SCORE, reversed)
             );

            TopDocsCollector<Entry> topCollector = TopFieldCollector.Create(sort, 50, false, true, true, true);
            searcher.Search(newq, null, topCollector);

            TopDocs topDocs = topCollector.TopDocs(0, 10);
            int nDocsReturned = topDocs.ScoreDocs.Length;

            Assert.AreEqual(4, nDocsReturned);

            // 0 & 3 were elevated
            Assert.AreEqual(0, topDocs.ScoreDocs[0].Doc);
            Assert.AreEqual(3, topDocs.ScoreDocs[1].Doc);

            if (reversed)
            {
                Assert.AreEqual(2, topDocs.ScoreDocs[2].Doc);
                Assert.AreEqual(1, topDocs.ScoreDocs[3].Doc);
            }
            else
            {
                Assert.AreEqual(1, topDocs.ScoreDocs[2].Doc);
                Assert.AreEqual(2, topDocs.ScoreDocs[3].Doc);
            }

            /*
            for (int i = 0; i < nDocsReturned; i++) {
             ScoreDoc scoreDoc = topDocs.ScoreDocs[i];
             ids[i] = scoreDoc.Doc;
             scores[i] = scoreDoc.Score;
             documents[i] = searcher.Doc(ids[i]);
             System.out.println("ids[i] = " + ids[i]);
             System.out.println("documents[i] = " + documents[i]);
             System.out.println("scores[i] = " + scores[i]);
           }
            */
        }

        private Query GetElevatedQuery(string[] vals)
        {
            BooleanQuery q = new BooleanQuery(false);
            q.Boost = 0;
            int max = (vals.Length / 2) + 5;
            for (int i = 0; i < vals.Length - 1; i += 2)
            {
                q.Add(new TermQuery(new Term(vals[i], vals[i + 1])), BooleanClause.Occur.SHOULD);
                Priority[new BytesRef(vals[i + 1])] = Convert.ToInt32(max--);
                // System.out.println(" pri doc=" + vals[i+1] + " pri=" + (1+max));
            }
            return q;
        }

        private Document Adoc(string[] vals)
        {
            Document doc = new Document();
            for (int i = 0; i < vals.Length - 2; i += 2)
            {
                doc.Add(NewTextField(vals[i], vals[i + 1], Field.Store.YES));
            }
            return doc;
        }
    }

    internal class ElevationComparatorSource : FieldComparatorSource
    {
        private readonly IDictionary<BytesRef, int?> Priority;

        public ElevationComparatorSource(IDictionary<BytesRef, int?> boosts)
        {
            this.Priority = boosts;
        }

        public override FieldComparator NewComparator(string fieldname, int numHits, int sortPos, bool reversed)
        {
            return new FieldComparatorAnonymousInnerClassHelper(this, fieldname, numHits);
        }

        private class FieldComparatorAnonymousInnerClassHelper : FieldComparator
        {
            private readonly ElevationComparatorSource OuterInstance;

            private string Fieldname;
            private int NumHits;

            public FieldComparatorAnonymousInnerClassHelper(ElevationComparatorSource outerInstance, string fieldname, int numHits)
            {
                this.OuterInstance = outerInstance;
                this.Fieldname = fieldname;
                this.NumHits = numHits;
                values = new int[numHits];
                tempBR = new BytesRef();
            }

            internal SortedDocValues idIndex;
            private readonly int[] values;
            private readonly BytesRef tempBR;
            internal int bottomVal;

            public override int Compare(int slot1, int slot2)
            {
                return values[slot2] - values[slot1]; // values will be small enough that there is no overflow concern
            }

            public override int Bottom
            {
                set
                {
                    bottomVal = values[value];
                }
            }

            public override object TopValue
            {
                set
                {
                    throw new System.NotSupportedException();
                }
            }

            private int DocVal(int doc)
            {
                int ord = idIndex.GetOrd(doc);
                if (ord == -1)
                {
                    return 0;
                }
                else
                {
                    idIndex.LookupOrd(ord, tempBR);
                    int? prio;
                    if (OuterInstance.Priority.TryGetValue(tempBR, out prio))
                    {
                        return (int)prio;
                    }
                    return 0;
                }
            }

            public override int CompareBottom(int doc)
            {
                return DocVal(doc) - bottomVal;
            }

            public override void Copy(int slot, int doc)
            {
                values[slot] = DocVal(doc);
            }

            public override FieldComparator SetNextReader(AtomicReaderContext context)
            {
                idIndex = FieldCache_Fields.DEFAULT.GetTermsIndex((AtomicReader)context.Reader(), Fieldname);
                return this;
            }

            public override IComparable Value(int slot)
            {
                return Convert.ToInt32(values[slot]);
            }

            public override int CompareTop(int doc)
            {
                throw new System.NotSupportedException();
            }
        }
    }
}