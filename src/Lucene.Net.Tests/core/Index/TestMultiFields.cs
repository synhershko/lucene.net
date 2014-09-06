using Lucene.Net.Randomized.Generators;
using System;
using System.Collections.Generic;

namespace Lucene.Net.Index
{
    using Lucene.Net.Analysis;
    using Lucene.Net.Document;
    using Lucene.Net.Store;
    using Lucene.Net.Util;
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

    using DocIdSetIterator = Lucene.Net.Search.DocIdSetIterator;

    [TestFixture]
    public class TestMultiFields : LuceneTestCase
    {
        [Test]
        public virtual void TestRandom()
        {
            int num = AtLeast(2);
            for (int iter = 0; iter < num; iter++)
            {
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: iter=" + iter);
                }

                Directory dir = NewDirectory();

                IndexWriter w = new IndexWriter(dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetMergePolicy(NoMergePolicy.COMPOUND_FILES));
                // we can do this because we use NoMergePolicy (and dont merge to "nothing")
                w.KeepFullyDeletedSegments = true;

                IDictionary<BytesRef, IList<int?>> docs = new Dictionary<BytesRef, IList<int?>>();
                HashSet<int?> deleted = new HashSet<int?>();
                IList<BytesRef> terms = new List<BytesRef>();

                int numDocs = TestUtil.NextInt(Random(), 1, 100 * RANDOM_MULTIPLIER);
                Document doc = new Document();
                Field f = NewStringField("field", "", Field.Store.NO);
                doc.Add(f);
                Field id = NewStringField("id", "", Field.Store.NO);
                doc.Add(id);

                bool onlyUniqueTerms = Random().NextBoolean();
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: onlyUniqueTerms=" + onlyUniqueTerms + " numDocs=" + numDocs);
                }
                HashSet<BytesRef> uniqueTerms = new HashSet<BytesRef>();
                for (int i = 0; i < numDocs; i++)
                {
                    if (!onlyUniqueTerms && Random().NextBoolean() && terms.Count > 0)
                    {
                        // re-use existing term
                        BytesRef term = terms[Random().Next(terms.Count)];
                        docs[term].Add(i);
                        f.StringValue = term.Utf8ToString();
                    }
                    else
                    {
                        string s = TestUtil.RandomUnicodeString(Random(), 10);
                        BytesRef term = new BytesRef(s);
                        if (!docs.ContainsKey(term))
                        {
                            docs[term] = new List<int?>();
                        }
                        docs[term].Add(i);
                        terms.Add(term);
                        uniqueTerms.Add(term);
                        f.StringValue = s;
                    }
                    id.StringValue = "" + i;
                    w.AddDocument(doc);
                    if (Random().Next(4) == 1)
                    {
                        w.Commit();
                    }
                    if (i > 0 && Random().Next(20) == 1)
                    {
                        int delID = Random().Next(i);
                        deleted.Add(delID);
                        w.DeleteDocuments(new Term("id", "" + delID));
                        if (VERBOSE)
                        {
                            Console.WriteLine("TEST: delete " + delID);
                        }
                    }
                }

                if (VERBOSE)
                {
                    List<BytesRef> termsList = new List<BytesRef>(uniqueTerms);
                    termsList.Sort(BytesRef.UTF8SortedAsUTF16Comparer);
                    Console.WriteLine("TEST: terms in UTF16 order:");
                    foreach (BytesRef b in termsList)
                    {
                        Console.WriteLine("  " + UnicodeUtil.ToHexString(b.Utf8ToString()) + " " + b);
                        foreach (int docID in docs[b])
                        {
                            if (deleted.Contains(docID))
                            {
                                Console.WriteLine("    " + docID + " (deleted)");
                            }
                            else
                            {
                                Console.WriteLine("    " + docID);
                            }
                        }
                    }
                }

                IndexReader reader = w.Reader;
                w.Dispose();
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: reader=" + reader);
                }

                Bits liveDocs = MultiFields.GetLiveDocs(reader);
                foreach (int delDoc in deleted)
                {
                    Assert.IsFalse(liveDocs.Get(delDoc));
                }

                for (int i = 0; i < 100; i++)
                {
                    BytesRef term = terms[Random().Next(terms.Count)];
                    if (VERBOSE)
                    {
                        Console.WriteLine("TEST: seek term=" + UnicodeUtil.ToHexString(term.Utf8ToString()) + " " + term);
                    }

                    DocsEnum docsEnum = TestUtil.Docs(Random(), reader, "field", term, liveDocs, null, DocsEnum.FLAG_NONE);
                    Assert.IsNotNull(docsEnum);

                    foreach (int docID in docs[term])
                    {
                        if (!deleted.Contains(docID))
                        {
                            Assert.AreEqual(docID, docsEnum.NextDoc());
                        }
                    }
                    Assert.AreEqual(DocIdSetIterator.NO_MORE_DOCS, docsEnum.NextDoc());
                }

                reader.Dispose();
                dir.Dispose();
            }
        }

        /*
        private void verify(IndexReader r, String term, List<Integer> expected) throws Exception {
          DocsEnum docs = TestUtil.Docs(random, r,
                                         "field",
                                         new BytesRef(term),
                                         MultiFields.GetLiveDocs(r),
                                         null,
                                         false);
          for(int docID : expected) {
            Assert.AreEqual(docID, docs.NextDoc());
          }
          Assert.AreEqual(docs.NO_MORE_DOCS, docs.NextDoc());
        }
        */

        [Test]
        public virtual void TestSeparateEnums()
        {
            Directory dir = NewDirectory();
            IndexWriter w = new IndexWriter(dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())));
            Document d = new Document();
            d.Add(NewStringField("f", "j", Field.Store.NO));
            w.AddDocument(d);
            w.Commit();
            w.AddDocument(d);
            IndexReader r = w.Reader;
            w.Dispose();
            DocsEnum d1 = TestUtil.Docs(Random(), r, "f", new BytesRef("j"), null, null, DocsEnum.FLAG_NONE);
            DocsEnum d2 = TestUtil.Docs(Random(), r, "f", new BytesRef("j"), null, null, DocsEnum.FLAG_NONE);
            Assert.AreEqual(0, d1.NextDoc());
            Assert.AreEqual(0, d2.NextDoc());
            r.Dispose();
            dir.Dispose();
        }

        [Test]
        public virtual void TestTermDocsEnum()
        {
            Directory dir = NewDirectory();
            IndexWriter w = new IndexWriter(dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())));
            Document d = new Document();
            d.Add(NewStringField("f", "j", Field.Store.NO));
            w.AddDocument(d);
            w.Commit();
            w.AddDocument(d);
            IndexReader r = w.Reader;
            w.Dispose();
            DocsEnum de = MultiFields.GetTermDocsEnum(r, null, "f", new BytesRef("j"));
            Assert.AreEqual(0, de.NextDoc());
            Assert.AreEqual(1, de.NextDoc());
            Assert.AreEqual(DocIdSetIterator.NO_MORE_DOCS, de.NextDoc());
            r.Dispose();
            dir.Dispose();
        }
    }
}