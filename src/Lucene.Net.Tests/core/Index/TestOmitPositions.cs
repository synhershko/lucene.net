namespace Lucene.Net.Index
{
    using Lucene.Net.Randomized.Generators;
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
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Directory = Lucene.Net.Store.Directory;
    using DocIdSetIterator = Lucene.Net.Search.DocIdSetIterator;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FieldType = Lucene.Net.Document.FieldType;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using TestUtil = Lucene.Net.Util.TestUtil;
    using TextField = Lucene.Net.Document.TextField;

    ///
    /// <summary>
    /// @lucene.experimental
    /// </summary>
    [TestFixture]
    public class TestOmitPositions : LuceneTestCase
    {
        [Test]
        public virtual void TestBasic()
        {
            Directory dir = NewDirectory();
            RandomIndexWriter w = new RandomIndexWriter(Random(), dir);
            Document doc = new Document();
            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            ft.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_AND_FREQS;
            Field f = NewField("foo", "this is a test test", ft);
            doc.Add(f);
            for (int i = 0; i < 100; i++)
            {
                w.AddDocument(doc);
            }

            IndexReader reader = w.Reader;
            w.Dispose();

            Assert.IsNull(MultiFields.GetTermPositionsEnum(reader, null, "foo", new BytesRef("test")));

            DocsEnum de = TestUtil.Docs(Random(), reader, "foo", new BytesRef("test"), null, null, DocsEnum.FLAG_FREQS);
            while (de.NextDoc() != DocIdSetIterator.NO_MORE_DOCS)
            {
                Assert.AreEqual(2, de.Freq());
            }

            reader.Dispose();
            dir.Dispose();
        }

        // Tests whether the DocumentWriter correctly enable the
        // omitTermFreqAndPositions bit in the FieldInfo
        [Test]
        public virtual void TestPositions()
        {
            Directory ram = NewDirectory();
            Analyzer analyzer = new MockAnalyzer(Random());
            IndexWriter writer = new IndexWriter(ram, NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer));
            Document d = new Document();

            // f1,f2,f3: docs only
            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            ft.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_ONLY;

            Field f1 = NewField("f1", "this field has docs only", ft);
            d.Add(f1);

            Field f2 = NewField("f2", "this field has docs only", ft);
            d.Add(f2);

            Field f3 = NewField("f3", "this field has docs only", ft);
            d.Add(f3);

            FieldType ft2 = new FieldType(TextField.TYPE_NOT_STORED);
            ft2.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_AND_FREQS;

            // f4,f5,f6 docs and freqs
            Field f4 = NewField("f4", "this field has docs and freqs", ft2);
            d.Add(f4);

            Field f5 = NewField("f5", "this field has docs and freqs", ft2);
            d.Add(f5);

            Field f6 = NewField("f6", "this field has docs and freqs", ft2);
            d.Add(f6);

            FieldType ft3 = new FieldType(TextField.TYPE_NOT_STORED);
            ft3.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS;

            // f7,f8,f9 docs/freqs/positions
            Field f7 = NewField("f7", "this field has docs and freqs and positions", ft3);
            d.Add(f7);

            Field f8 = NewField("f8", "this field has docs and freqs and positions", ft3);
            d.Add(f8);

            Field f9 = NewField("f9", "this field has docs and freqs and positions", ft3);
            d.Add(f9);

            writer.AddDocument(d);
            writer.ForceMerge(1);

            // now we add another document which has docs-only for f1, f4, f7, docs/freqs for f2, f5, f8,
            // and docs/freqs/positions for f3, f6, f9
            d = new Document();

            // f1,f4,f7: docs only
            f1 = NewField("f1", "this field has docs only", ft);
            d.Add(f1);

            f4 = NewField("f4", "this field has docs only", ft);
            d.Add(f4);

            f7 = NewField("f7", "this field has docs only", ft);
            d.Add(f7);

            // f2, f5, f8: docs and freqs
            f2 = NewField("f2", "this field has docs and freqs", ft2);
            d.Add(f2);

            f5 = NewField("f5", "this field has docs and freqs", ft2);
            d.Add(f5);

            f8 = NewField("f8", "this field has docs and freqs", ft2);
            d.Add(f8);

            // f3, f6, f9: docs and freqs and positions
            f3 = NewField("f3", "this field has docs and freqs and positions", ft3);
            d.Add(f3);

            f6 = NewField("f6", "this field has docs and freqs and positions", ft3);
            d.Add(f6);

            f9 = NewField("f9", "this field has docs and freqs and positions", ft3);
            d.Add(f9);

            writer.AddDocument(d);

            // force merge
            writer.ForceMerge(1);
            // flush
            writer.Dispose();

            SegmentReader reader = GetOnlySegmentReader(DirectoryReader.Open(ram));
            FieldInfos fi = reader.FieldInfos;
            // docs + docs = docs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f1").FieldIndexOptions);
            // docs + docs/freqs = docs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f2").FieldIndexOptions);
            // docs + docs/freqs/pos = docs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f3").FieldIndexOptions);
            // docs/freqs + docs = docs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f4").FieldIndexOptions);
            // docs/freqs + docs/freqs = docs/freqs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_AND_FREQS, fi.FieldInfo("f5").FieldIndexOptions);
            // docs/freqs + docs/freqs/pos = docs/freqs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_AND_FREQS, fi.FieldInfo("f6").FieldIndexOptions);
            // docs/freqs/pos + docs = docs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_ONLY, fi.FieldInfo("f7").FieldIndexOptions);
            // docs/freqs/pos + docs/freqs = docs/freqs
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_AND_FREQS, fi.FieldInfo("f8").FieldIndexOptions);
            // docs/freqs/pos + docs/freqs/pos = docs/freqs/pos
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS, fi.FieldInfo("f9").FieldIndexOptions);

            reader.Dispose();
            ram.Dispose();
        }

        private void AssertNoPrx(Directory dir)
        {
            string[] files = dir.ListAll();
            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsFalse(files[i].EndsWith(".prx"));
                Assert.IsFalse(files[i].EndsWith(".pos"));
            }
        }

        // Verifies no *.prx exists when all fields omit term positions:
        [Test]
        public virtual void TestNoPrxFile()
        {
            Directory ram = NewDirectory();
            Analyzer analyzer = new MockAnalyzer(Random());
            IndexWriter writer = new IndexWriter(ram, NewIndexWriterConfig(TEST_VERSION_CURRENT, analyzer).SetMaxBufferedDocs(3).SetMergePolicy(NewLogMergePolicy()));
            LogMergePolicy lmp = (LogMergePolicy)writer.Config.MergePolicy;
            lmp.MergeFactor = 2;
            lmp.NoCFSRatio = 0.0;
            Document d = new Document();

            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            ft.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_AND_FREQS;
            Field f1 = NewField("f1", "this field has term freqs", ft);
            d.Add(f1);

            for (int i = 0; i < 30; i++)
            {
                writer.AddDocument(d);
            }

            writer.Commit();

            AssertNoPrx(ram);

            // now add some documents with positions, and check there is no prox after optimization
            d = new Document();
            f1 = NewTextField("f1", "this field has positions", Field.Store.NO);
            d.Add(f1);

            for (int i = 0; i < 30; i++)
            {
                writer.AddDocument(d);
            }

            // force merge
            writer.ForceMerge(1);
            // flush
            writer.Dispose();

            AssertNoPrx(ram);
            ram.Dispose();
        }

        /// <summary>
        /// make sure we downgrade positions and payloads correctly </summary>
        [Test]
        public virtual void TestMixing()
        {
            // no positions
            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            ft.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_AND_FREQS;

            Directory dir = NewDirectory();
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir);

            for (int i = 0; i < 20; i++)
            {
                Document doc = new Document();
                if (i < 19 && Random().NextBoolean())
                {
                    for (int j = 0; j < 50; j++)
                    {
                        doc.Add(new TextField("foo", "i have positions", Field.Store.NO));
                    }
                }
                else
                {
                    for (int j = 0; j < 50; j++)
                    {
                        doc.Add(new Field("foo", "i have no positions", ft));
                    }
                }
                iw.AddDocument(doc);
                iw.Commit();
            }

            if (Random().NextBoolean())
            {
                iw.ForceMerge(1);
            }

            DirectoryReader ir = iw.Reader;
            FieldInfos fis = MultiFields.GetMergedFieldInfos(ir);
            Assert.AreEqual(FieldInfo.IndexOptions.DOCS_AND_FREQS, fis.FieldInfo("foo").FieldIndexOptions);
            Assert.IsFalse(fis.FieldInfo("foo").HasPayloads());
            iw.Dispose();
            ir.Dispose();
            dir.Dispose(); // checkindex
        }
    }
}