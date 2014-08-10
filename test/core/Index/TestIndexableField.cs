using Lucene.Net.Document;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Lucene.Net.Index
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
    using BooleanClause = Lucene.Net.Search.BooleanClause;
    using BooleanQuery = Lucene.Net.Search.BooleanQuery;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Codec = Lucene.Net.Codecs.Codec;
    using Directory = Lucene.Net.Store.Directory;
    using DocIdSetIterator = Lucene.Net.Search.DocIdSetIterator;
    using Document = Lucene.Net.Document.Document;
    using DocValuesType = Lucene.Net.Index.FieldInfo.DocValuesType_e;
    using Field = Lucene.Net.Document.Field;
    using IndexSearcher = Lucene.Net.Search.IndexSearcher;
    using Lucene3xCodec = Lucene.Net.Codecs.Lucene3x.Lucene3xCodec;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using TermQuery = Lucene.Net.Search.TermQuery;
    using TestUtil = Lucene.Net.Util.TestUtil;
    using TokenStream = Lucene.Net.Analysis.TokenStream;
    using TopDocs = Lucene.Net.Search.TopDocs;

    [TestFixture]
    public class TestIndexableField : LuceneTestCase
    {
        private class MyField : IndexableField
        {
            private readonly TestIndexableField OuterInstance;

            internal readonly int Counter;
            internal readonly IndexableFieldType fieldType;

            public MyField()
            {
                fieldType = new IndexableFieldTypeAnonymousInnerClassHelper(this);
            }

            private class IndexableFieldTypeAnonymousInnerClassHelper : IndexableFieldType
            {
                private MyField OuterInstance;

                public IndexableFieldTypeAnonymousInnerClassHelper(MyField outerInstance)
                {
                    OuterInstance = outerInstance;
                }

                public bool Indexed
                {
                    get { return (OuterInstance.Counter % 10) != 3; }
                    set { }
                }

                public bool Stored
                {
                    get { return (OuterInstance.Counter & 1) == 0 || (OuterInstance.Counter % 10) == 3; }
                    set { }
                }

                public bool Tokenized
                {
                    get { return true; }
                    set { }
                }

                public bool StoreTermVectors
                {
                    get { return Indexed && OuterInstance.Counter % 2 == 1 && OuterInstance.Counter % 10 != 9; }
                    set { }
                }

                public bool StoreTermVectorOffsets
                {
                    get { return StoreTermVectors && OuterInstance.Counter % 10 != 9; }
                    set { }
                }

                public bool StoreTermVectorPositions
                {
                    get { return StoreTermVectors && OuterInstance.Counter % 10 != 9; }
                    set { }
                }

                public bool StoreTermVectorPayloads
                {
                    get
                    {
                        if (Codec.Default is Lucene3xCodec)
                        {
                            return false; // 3.x doesnt support
                        }
                        else
                        {
                            return StoreTermVectors && OuterInstance.Counter % 10 != 9;
                        }
                    }
                    set { }
                }

                public bool OmitNorms
                {
                    get { return false; }
                    set { }
                }

                public FieldInfo.IndexOptions? IndexOptionsValue
                {
                    get { return FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS; }
                    set { }
                }

                public FieldType.NumericType? NumericTypeValue
                {
                    get { throw new NotImplementedException(); }
                    set { }
                }

                public DocValuesType? DocValueType
                {
                    get { return null; }
                    set { }
                }
            }

            public MyField(TestIndexableField outerInstance, int counter)
                : this()
            {
                this.OuterInstance = outerInstance;
                this.Counter = counter;
            }

            public string Name()
            {
                return "f" + Counter;
            }

            public float GetBoost()
            {
                return 1.0f + (float)Random().NextDouble();
            }

            public BytesRef BinaryValue()
            {
                if ((Counter % 10) == 3)
                {
                    sbyte[] bytes = new sbyte[10];
                    for (int idx = 0; idx < bytes.Length; idx++)
                    {
                        bytes[idx] = (sbyte)(Counter + idx);
                    }
                    return new BytesRef(bytes, 0, bytes.Length);
                }
                else
                {
                    return null;
                }
            }

            public string StringValue
            {
                get
                {
                    int fieldID = Counter % 10;
                    if (fieldID != 3 && fieldID != 7)
                    {
                        return "text " + Counter;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            public TextReader ReaderValue
            {
                get
                {
                    if (Counter % 10 == 7)
                    {
                        return new StringReader("text " + Counter);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            public object NumericValue
            {
                get { return null; }
            }

            public IndexableFieldType FieldType()
            {
                return fieldType;
            }

            public TokenStream GetTokenStream(Analyzer analyzer)
            {
                return ReaderValue != null ? analyzer.TokenStream(Name(), ReaderValue) : analyzer.TokenStream(Name(), new StringReader(StringValue));
            }
        }

        // Silly test showing how to index documents w/o using Lucene's core
        // Document nor Field class
        [Test]
        public virtual void TestArbitraryFields()
        {
            Directory dir = NewDirectory();
            RandomIndexWriter w = new RandomIndexWriter(Random(), dir);

            int NUM_DOCS = AtLeast(27);
            if (VERBOSE)
            {
                Console.WriteLine("TEST: " + NUM_DOCS + " docs");
            }
            int[] fieldsPerDoc = new int[NUM_DOCS];
            int baseCount = 0;

            for (int docCount = 0; docCount < NUM_DOCS; docCount++)
            {
                int fieldCount = TestUtil.NextInt(Random(), 1, 17);
                fieldsPerDoc[docCount] = fieldCount - 1;

                int finalDocCount = docCount;
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: " + fieldCount + " fields in doc " + docCount);
                }

                int finalBaseCount = baseCount;
                baseCount += fieldCount - 1;

                w.AddDocument(new IterableAnonymousInnerClassHelper(this, fieldCount, finalDocCount, finalBaseCount));
            }

            IndexReader r = w.Reader;
            w.Dispose();

            IndexSearcher s = NewSearcher(r);
            int counter = 0;
            for (int id = 0; id < NUM_DOCS; id++)
            {
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: verify doc id=" + id + " (" + fieldsPerDoc[id] + " fields) counter=" + counter);
                }
                TopDocs hits = s.Search(new TermQuery(new Term("id", "" + id)), 1);
                Assert.AreEqual(1, hits.TotalHits);
                int docID = hits.ScoreDocs[0].Doc;
                Document doc = s.Doc(docID);
                int endCounter = counter + fieldsPerDoc[id];
                while (counter < endCounter)
                {
                    string name = "f" + counter;
                    int fieldID = counter % 10;

                    bool stored = (counter & 1) == 0 || fieldID == 3;
                    bool binary = fieldID == 3;
                    bool indexed = fieldID != 3;

                    string stringValue;
                    if (fieldID != 3 && fieldID != 9)
                    {
                        stringValue = "text " + counter;
                    }
                    else
                    {
                        stringValue = null;
                    }

                    // stored:
                    if (stored)
                    {
                        IndexableField f = doc.GetField(name);
                        Assert.IsNotNull(f, "doc " + id + " doesn't have field f" + counter);
                        if (binary)
                        {
                            Assert.IsNotNull(f, "doc " + id + " doesn't have field f" + counter);
                            BytesRef b = f.BinaryValue();
                            Assert.IsNotNull(b);
                            Assert.AreEqual(10, b.Length);
                            for (int idx = 0; idx < 10; idx++)
                            {
                                Assert.AreEqual((sbyte)(idx + counter), b.Bytes[b.Offset + idx]);
                            }
                        }
                        else
                        {
                            Debug.Assert(stringValue != null);
                            Assert.AreEqual(stringValue, f.StringValue);
                        }
                    }

                    if (indexed)
                    {
                        bool tv = counter % 2 == 1 && fieldID != 9;
                        if (tv)
                        {
                            Terms tfv = r.GetTermVectors(docID).Terms(name);
                            Assert.IsNotNull(tfv);
                            TermsEnum termsEnum = tfv.Iterator(null);
                            Assert.AreEqual(new BytesRef("" + counter), termsEnum.Next());
                            Assert.AreEqual(1, termsEnum.TotalTermFreq());
                            DocsAndPositionsEnum dpEnum = termsEnum.DocsAndPositions(null, null);
                            Assert.IsTrue(dpEnum.NextDoc() != DocIdSetIterator.NO_MORE_DOCS);
                            Assert.AreEqual(1, dpEnum.Freq());
                            Assert.AreEqual(1, dpEnum.NextPosition());

                            Assert.AreEqual(new BytesRef("text"), termsEnum.Next());
                            Assert.AreEqual(1, termsEnum.TotalTermFreq());
                            dpEnum = termsEnum.DocsAndPositions(null, dpEnum);
                            Assert.IsTrue(dpEnum.NextDoc() != DocIdSetIterator.NO_MORE_DOCS);
                            Assert.AreEqual(1, dpEnum.Freq());
                            Assert.AreEqual(0, dpEnum.NextPosition());

                            Assert.IsNull(termsEnum.Next());

                            // TODO: offsets
                        }
                        else
                        {
                            Fields vectors = r.GetTermVectors(docID);
                            Assert.IsTrue(vectors == null || vectors.Terms(name) == null);
                        }

                        BooleanQuery bq = new BooleanQuery();
                        bq.Add(new TermQuery(new Term("id", "" + id)), BooleanClause.Occur.MUST);
                        bq.Add(new TermQuery(new Term(name, "text")), BooleanClause.Occur.MUST);
                        TopDocs hits2 = s.Search(bq, 1);
                        Assert.AreEqual(1, hits2.TotalHits);
                        Assert.AreEqual(docID, hits2.ScoreDocs[0].Doc);

                        bq = new BooleanQuery();
                        bq.Add(new TermQuery(new Term("id", "" + id)), BooleanClause.Occur.MUST);
                        bq.Add(new TermQuery(new Term(name, "" + counter)), BooleanClause.Occur.MUST);
                        TopDocs hits3 = s.Search(bq, 1);
                        Assert.AreEqual(1, hits3.TotalHits);
                        Assert.AreEqual(docID, hits3.ScoreDocs[0].Doc);
                    }

                    counter++;
                }
            }

            r.Dispose();
            dir.Dispose();
        }

        private class IterableAnonymousInnerClassHelper : IEnumerable<IndexableField>
        {
            private readonly TestIndexableField OuterInstance;

            private int FieldCount;
            private int FinalDocCount;
            private int FinalBaseCount;

            public IterableAnonymousInnerClassHelper(TestIndexableField outerInstance, int fieldCount, int finalDocCount, int finalBaseCount)
            {
                this.OuterInstance = outerInstance;
                this.FieldCount = fieldCount;
                this.FinalDocCount = finalDocCount;
                this.FinalBaseCount = finalBaseCount;
            }

            public virtual IEnumerator<IndexableField> GetEnumerator()
            {
                return new IteratorAnonymousInnerClassHelper(this, OuterInstance);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private class IteratorAnonymousInnerClassHelper : IEnumerator<IndexableField>
            {
                private readonly IterableAnonymousInnerClassHelper OuterInstance;
                private readonly TestIndexableField OuterTextIndexableField;

                public IteratorAnonymousInnerClassHelper(IterableAnonymousInnerClassHelper outerInstance, TestIndexableField outerTextIndexableField)
                {
                    this.OuterInstance = outerInstance;
                    OuterTextIndexableField = outerTextIndexableField;
                }

                internal int fieldUpto;
                private IndexableField current;

                public bool MoveNext()
                {
                    if (fieldUpto >= OuterInstance.FieldCount)
                    {
                        return false;
                    }

                    Debug.Assert(fieldUpto < OuterInstance.FieldCount);
                    if (fieldUpto == 0)
                    {
                        fieldUpto = 1;
                        current = NewStringField("id", "" + OuterInstance.FinalDocCount, Field.Store.YES);
                    }
                    else
                    {
                        current = new MyField(OuterTextIndexableField, OuterInstance.FinalBaseCount + (fieldUpto++ - 1));
                    }

                    return true;
                }

                public IndexableField Current
                {
                    get { return current; }
                }

                object System.Collections.IEnumerator.Current
                {
                    get { return Current; }
                }

                public void Dispose()
                {
                }

                public void Reset()
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}