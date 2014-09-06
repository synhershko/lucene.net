using NUnit.Framework;
using System;
using System.Text;

namespace Lucene.Net.Codecs.Lucene41
{
    using Directory = Lucene.Net.Store.Directory;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using FieldType = Lucene.Net.Document.FieldType;
    using IndexableField = Lucene.Net.Index.IndexableField;
    using IndexWriter = Lucene.Net.Index.IndexWriter;
    using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
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
    using TestUtil = Lucene.Net.Util.TestUtil;
    using TextField = Lucene.Net.Document.TextField;

    /// <summary>
    /// Tests special cases of BlockPostingsFormat
    /// </summary>
    [TestFixture]
    public class TestBlockPostingsFormat2 : LuceneTestCase
    {
        internal Directory Dir;
        internal RandomIndexWriter Iw;
        internal IndexWriterConfig Iwc;

        [TestFixtureSetUp]
        public override void SetUp()
        {
            base.SetUp();
            Dir = NewFSDirectory(CreateTempDir("testDFBlockSize"));
            Iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
            Iwc.SetCodec(TestUtil.AlwaysPostingsFormat(new Lucene41PostingsFormat()));
            Iw = new RandomIndexWriter(Random(), Dir, (IndexWriterConfig)Iwc.Clone());
            Iw.RandomForceMerge = false; // we will ourselves
        }

        [TestFixtureTearDown]
        public override void TearDown()
        {
            Iw.Dispose();
            TestUtil.CheckIndex(Dir); // for some extra coverage, checkIndex before we forceMerge
            Iwc.SetOpenMode(IndexWriterConfig.OpenMode_e.APPEND);
            IndexWriter iw = new IndexWriter(Dir, (IndexWriterConfig)Iwc.Clone());
            iw.ForceMerge(1);
            iw.Dispose();
            Dir.Dispose(); // just force a checkindex for now
            base.TearDown();
        }

        private Document NewDocument()
        {
            Document doc = new Document();
            foreach (FieldInfo.IndexOptions option in Enum.GetValues(typeof(FieldInfo.IndexOptions)))
            {
                var ft = new FieldType(TextField.TYPE_NOT_STORED)
                {
                    StoreTermVectors = true,
                    StoreTermVectorOffsets = true,
                    StoreTermVectorPositions = true,
                    StoreTermVectorPayloads = true,
                    IndexOptionsValue = option
                };
                // turn on tvs for a cross-check, since we rely upon checkindex in this test (for now)
                doc.Add(new Field(option.ToString(), "", ft));
            }
            return doc;
        }

        /// <summary>
        /// tests terms with df = blocksize </summary>
        [Test]
        public virtual void TestDFBlockSize()
        {
            Document doc = NewDocument();
            for (int i = 0; i < Lucene41PostingsFormat.BLOCK_SIZE; i++)
            {
                foreach (IndexableField f in doc.Fields)
                {
                    ((Field)f).StringValue = f.Name() + " " + f.Name() + "_2";
                }
                Iw.AddDocument(doc);
            }
        }

        /// <summary>
        /// tests terms with df % blocksize = 0 </summary>
        [Test]
        public virtual void TestDFBlockSizeMultiple()
        {
            Document doc = NewDocument();
            for (int i = 0; i < Lucene41PostingsFormat.BLOCK_SIZE * 16; i++)
            {
                foreach (IndexableField f in doc.Fields)
                {
                    ((Field)f).StringValue = f.Name() + " " + f.Name() + "_2";
                }
                Iw.AddDocument(doc);
            }
        }

        /// <summary>
        /// tests terms with ttf = blocksize </summary>
        [Test]
        public virtual void TestTTFBlockSize()
        {
            Document doc = NewDocument();
            for (int i = 0; i < Lucene41PostingsFormat.BLOCK_SIZE / 2; i++)
            {
                foreach (IndexableField f in doc.Fields)
                {
                    ((Field)f).StringValue = f.Name() + " " + f.Name() + " " + f.Name() + "_2 " + f.Name() + "_2";
                }
                Iw.AddDocument(doc);
            }
        }

        /// <summary>
        /// tests terms with ttf % blocksize = 0 </summary>
        [Test]
        public virtual void TestTTFBlockSizeMultiple()
        {
            Document doc = NewDocument();
            for (int i = 0; i < Lucene41PostingsFormat.BLOCK_SIZE / 2; i++)
            {
                foreach (IndexableField f in doc.Fields)
                {
                    string proto = (f.Name() + " " + f.Name() + " " + f.Name() + " " + f.Name() + " " + f.Name() + "_2 " + f.Name() + "_2 " + f.Name() + "_2 " + f.Name() + "_2");
                    StringBuilder val = new StringBuilder();
                    for (int j = 0; j < 16; j++)
                    {
                        val.Append(proto);
                        val.Append(" ");
                    }
                    ((Field)f).StringValue = val.ToString();
                }
                Iw.AddDocument(doc);
            }
        }
    }
}