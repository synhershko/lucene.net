using System;

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

namespace Lucene.Net.Search
{
    using NUnit.Framework;
    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
    using Bits = Lucene.Net.Util.Bits;
    using Directory = Lucene.Net.Store.Directory;
    using DirectoryReader = Lucene.Net.Index.DirectoryReader;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FixedBitSet = Lucene.Net.Util.FixedBitSet;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using IndexWriter = Lucene.Net.Index.IndexWriter;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using OpenMode = Lucene.Net.Index.IndexWriterConfig.OpenMode_e;
    using Term = Lucene.Net.Index.Term;

    [TestFixture]
    public class TestFilteredSearch : LuceneTestCase
    {
        private const string FIELD = "category";

        [Test]
        public virtual void TestFilteredSearch_Mem()
        {
            bool enforceSingleSegment = true;
            Directory directory = NewDirectory();
            int[] filterBits = new int[] { 1, 36 };
            SimpleDocIdSetFilter filter = new SimpleDocIdSetFilter(filterBits);
            IndexWriter writer = new IndexWriter(directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMergePolicy(NewLogMergePolicy()));
            SearchFiltered(writer, directory, filter, enforceSingleSegment);
            // run the test on more than one segment
            enforceSingleSegment = false;
            writer.Dispose();
            writer = new IndexWriter(directory, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(OpenMode.CREATE).SetMaxBufferedDocs(10).SetMergePolicy(NewLogMergePolicy()));
            // we index 60 docs - this will create 6 segments
            SearchFiltered(writer, directory, filter, enforceSingleSegment);
            writer.Dispose();
            directory.Dispose();
        }

        public virtual void SearchFiltered(IndexWriter writer, Directory directory, Filter filter, bool fullMerge)
        {
            for (int i = 0; i < 60; i++) //Simple docs
            {
                Document doc = new Document();
                doc.Add(NewStringField(FIELD, Convert.ToString(i), Field.Store.YES));
                writer.AddDocument(doc);
            }
            if (fullMerge)
            {
                writer.ForceMerge(1);
            }
            writer.Dispose();

            BooleanQuery booleanQuery = new BooleanQuery();
            booleanQuery.Add(new TermQuery(new Term(FIELD, "36")), BooleanClause.Occur.SHOULD);

            IndexReader reader = DirectoryReader.Open(directory);
            IndexSearcher indexSearcher = NewSearcher(reader);
            ScoreDoc[] hits = indexSearcher.Search(booleanQuery, filter, 1000).ScoreDocs;
            Assert.AreEqual(1, hits.Length, "Number of matched documents");
            reader.Dispose();
        }

        public sealed class SimpleDocIdSetFilter : Filter
        {
            internal readonly int[] Docs;

            public SimpleDocIdSetFilter(int[] docs)
            {
                this.Docs = docs;
            }

            public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
            {
                Assert.IsNull(acceptDocs, "acceptDocs should be null, as we have an index without deletions");
                FixedBitSet set = new FixedBitSet(context.Reader().MaxDoc());
                int docBase = context.DocBase;
                int limit = docBase + context.Reader().MaxDoc();
                for (int index = 0; index < Docs.Length; index++)
                {
                    int docId = Docs[index];
                    if (docId >= docBase && docId < limit)
                    {
                        set.Set(docId - docBase);
                    }
                }
                return set.Cardinality() == 0 ? null : set;
            }
        }
    }
}