using System;

namespace Lucene.Net.Codecs.Perfield
{
    using Lucene.Net.Randomized.Generators;
    using NUnit.Framework;
    using Directory = Lucene.Net.Store.Directory;
    using DirectoryReader = Lucene.Net.Index.DirectoryReader;

    /*using MockSepPostingsFormat = Lucene.Net.Codecs.mocksep.MockSepPostingsFormat;
        using Pulsing41PostingsFormat = Lucene.Net.Codecs.pulsing.Pulsing41PostingsFormat;
        using SimpleTextPostingsFormat = Lucene.Net.Codecs.simpletext.SimpleTextPostingsFormat;*/

    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FieldType = Lucene.Net.Document.FieldType;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using IndexSearcher = Lucene.Net.Search.IndexSearcher;
    using IndexWriter = Lucene.Net.Index.IndexWriter;
    using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
    using LogDocMergePolicy = Lucene.Net.Index.LogDocMergePolicy;
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
    using Term = Lucene.Net.Index.Term;
    using TermQuery = Lucene.Net.Search.TermQuery;
    using TestUtil = Lucene.Net.Util.TestUtil;
    using TextField = Lucene.Net.Document.TextField;
    using TopDocs = Lucene.Net.Search.TopDocs;

    //TODO: would be better in this test to pull termsenums and instanceof or something?
    // this way we can verify PFPF is doing the right thing.
    // for now we do termqueries.
    [TestFixture]
    public class TestPerFieldPostingsFormat2 : LuceneTestCase
    {
        private IndexWriter NewWriter(Directory dir, IndexWriterConfig conf)
        {
            LogDocMergePolicy logByteSizeMergePolicy = new LogDocMergePolicy();
            logByteSizeMergePolicy.NoCFSRatio = 0.0; // make sure we use plain
            // files
            conf.SetMergePolicy(logByteSizeMergePolicy);

            IndexWriter writer = new IndexWriter(dir, conf);
            return writer;
        }

        private void AddDocs(IndexWriter writer, int numDocs)
        {
            for (int i = 0; i < numDocs; i++)
            {
                Document doc = new Document();
                doc.Add(NewTextField("content", "aaa", Field.Store.NO));
                writer.AddDocument(doc);
            }
        }

        private void AddDocs2(IndexWriter writer, int numDocs)
        {
            for (int i = 0; i < numDocs; i++)
            {
                Document doc = new Document();
                doc.Add(NewTextField("content", "bbb", Field.Store.NO));
                writer.AddDocument(doc);
            }
        }

        private void AddDocs3(IndexWriter writer, int numDocs)
        {
            for (int i = 0; i < numDocs; i++)
            {
                Document doc = new Document();
                doc.Add(NewTextField("content", "ccc", Field.Store.NO));
                doc.Add(NewStringField("id", "" + i, Field.Store.YES));
                writer.AddDocument(doc);
            }
        }

        /*
         * Test that heterogeneous index segments are merge successfully
         */
        /*public virtual void TestMergeUnusedPerFieldCodec()
        {
          Directory dir = NewDirectory();
          IndexWriterConfig iwconf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode_e.CREATE).SetCodec(new MockCodec());
          IndexWriter writer = NewWriter(dir, iwconf);
          AddDocs(writer, 10);
          writer.Commit();
          AddDocs3(writer, 10);
          writer.Commit();
          AddDocs2(writer, 10);
          writer.Commit();
          Assert.AreEqual(30, writer.MaxDoc());
          TestUtil.CheckIndex(dir);
          writer.ForceMerge(1);
          Assert.AreEqual(30, writer.MaxDoc());
          writer.Dispose();
          dir.Dispose();
        }*/

        /*
         * Test that heterogeneous index segments are merged sucessfully
         */
        // TODO: not sure this test is that great, we should probably peek inside PerFieldPostingsFormat or something?!
        /*public virtual void TestChangeCodecAndMerge()
        {
          Directory dir = NewDirectory();
          if (VERBOSE)
          {
            Console.WriteLine("TEST: make new index");
          }
          IndexWriterConfig iwconf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode_e.CREATE).SetCodec(new MockCodec());
          iwconf.SetMaxBufferedDocs(IndexWriterConfig.DISABLE_AUTO_FLUSH);
          //((LogMergePolicy) iwconf.getMergePolicy()).setMergeFactor(10);
          IndexWriter writer = NewWriter(dir, iwconf);

          AddDocs(writer, 10);
          writer.Commit();
          AssertQuery(new Term("content", "aaa"), dir, 10);
          if (VERBOSE)
          {
            Console.WriteLine("TEST: addDocs3");
          }
          AddDocs3(writer, 10);
          writer.Commit();
          writer.Dispose();

          AssertQuery(new Term("content", "ccc"), dir, 10);
          AssertQuery(new Term("content", "aaa"), dir, 10);
          Codec codec = iwconf.Codec;

          iwconf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random())).SetOpenMode(IndexWriterConfig.OpenMode_e.APPEND).SetCodec(codec);
          //((LogMergePolicy) iwconf.getMergePolicy()).setNoCFSRatio(0.0);
          //((LogMergePolicy) iwconf.getMergePolicy()).setMergeFactor(10);
          iwconf.SetMaxBufferedDocs(IndexWriterConfig.DISABLE_AUTO_FLUSH);

          iwconf.SetCodec(new MockCodec2()); // uses standard for field content
          writer = NewWriter(dir, iwconf);
          // swap in new codec for currently written segments
          if (VERBOSE)
          {
            Console.WriteLine("TEST: add docs w/ Standard codec for content field");
          }
          AddDocs2(writer, 10);
          writer.Commit();
          codec = iwconf.Codec;
          Assert.AreEqual(30, writer.MaxDoc());
          AssertQuery(new Term("content", "bbb"), dir, 10);
          AssertQuery(new Term("content", "ccc"), dir, 10); ////
          AssertQuery(new Term("content", "aaa"), dir, 10);

          if (VERBOSE)
          {
            Console.WriteLine("TEST: add more docs w/ new codec");
          }
          AddDocs2(writer, 10);
          writer.Commit();
          AssertQuery(new Term("content", "ccc"), dir, 10);
          AssertQuery(new Term("content", "bbb"), dir, 20);
          AssertQuery(new Term("content", "aaa"), dir, 10);
          Assert.AreEqual(40, writer.MaxDoc());

          if (VERBOSE)
          {
            Console.WriteLine("TEST: now optimize");
          }
          writer.ForceMerge(1);
          Assert.AreEqual(40, writer.MaxDoc());
          writer.Dispose();
          AssertQuery(new Term("content", "ccc"), dir, 10);
          AssertQuery(new Term("content", "bbb"), dir, 20);
          AssertQuery(new Term("content", "aaa"), dir, 10);

          dir.Dispose();
        }*/

        public virtual void AssertQuery(Term t, Directory dir, int num)
        {
            if (VERBOSE)
            {
                Console.WriteLine("\nTEST: assertQuery " + t);
            }
            IndexReader reader = DirectoryReader.Open(dir, 1);
            IndexSearcher searcher = NewSearcher(reader);
            TopDocs search = searcher.Search(new TermQuery(t), num + 10);
            Assert.AreEqual(num, search.TotalHits);
            reader.Dispose();
        }

        /*public class MockCodec : Lucene46Codec
        {
          internal readonly PostingsFormat Lucene40 = new Lucene41PostingsFormat();
          internal readonly PostingsFormat SimpleText = new SimpleTextPostingsFormat();
          internal readonly PostingsFormat MockSep = new MockSepPostingsFormat();

          public override PostingsFormat GetPostingsFormatForField(string field)
          {
            if (field.Equals("id"))
            {
              return SimpleText;
            }
            else if (field.Equals("content"))
            {
              return MockSep;
            }
            else
            {
              return Lucene40;
            }
          }
        }*/

        /*public class MockCodec2 : Lucene46Codec
        {
          internal readonly PostingsFormat Lucene40 = new Lucene41PostingsFormat();
          internal readonly PostingsFormat SimpleText = new SimpleTextPostingsFormat();

          public override PostingsFormat GetPostingsFormatForField(string field)
          {
            if (field.Equals("id"))
            {
              return SimpleText;
            }
            else
            {
              return Lucene40;
            }
          }
        }*/

        /*
         * Test per field codec support - adding fields with random codecs
         */

        [Test]
        public virtual void TestStressPerFieldCodec()
        {
            Directory dir = NewDirectory(Random());
            const int docsPerRound = 97;
            int numRounds = AtLeast(1);
            for (int i = 0; i < numRounds; i++)
            {
                int num = TestUtil.NextInt(Random(), 30, 60);
                IndexWriterConfig config = NewIndexWriterConfig(Random(), TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
                config.SetOpenMode(IndexWriterConfig.OpenMode_e.CREATE_OR_APPEND);
                IndexWriter writer = NewWriter(dir, config);
                for (int j = 0; j < docsPerRound; j++)
                {
                    Document doc = new Document();
                    for (int k = 0; k < num; k++)
                    {
                        FieldType customType = new FieldType(TextField.TYPE_NOT_STORED);
                        customType.Tokenized = Random().NextBoolean();
                        customType.OmitNorms = Random().NextBoolean();
                        Field field = NewField("" + k, TestUtil.RandomRealisticUnicodeString(Random(), 128), customType);
                        doc.Add(field);
                    }
                    writer.AddDocument(doc);
                }
                if (Random().NextBoolean())
                {
                    writer.ForceMerge(1);
                }
                writer.Commit();
                Assert.AreEqual((i + 1) * docsPerRound, writer.MaxDoc());
                writer.Dispose();
            }
            dir.Dispose();
        }

        /*public virtual void TestSameCodecDifferentInstance()
        {
          Codec codec = new Lucene46CodecAnonymousInnerClassHelper(this);
          DoTestMixedPostings(codec);
        }*/

        /*private class Lucene46CodecAnonymousInnerClassHelper : Lucene46Codec
        {
            private readonly TestPerFieldPostingsFormat2 OuterInstance;

            public Lucene46CodecAnonymousInnerClassHelper(TestPerFieldPostingsFormat2 outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public override PostingsFormat GetPostingsFormatForField(string field)
            {
              if ("id".Equals(field))
              {
                return new Pulsing41PostingsFormat(1);
              }
              else if ("date".Equals(field))
              {
                return new Pulsing41PostingsFormat(1);
              }
              else
              {
                return base.GetPostingsFormatForField(field);
              }
            }
        }*/

        /*public virtual void TestSameCodecDifferentParams()
        {
          Codec codec = new Lucene46CodecAnonymousInnerClassHelper2(this);
          DoTestMixedPostings(codec);
        }*/

        /*private class Lucene46CodecAnonymousInnerClassHelper2 : Lucene46Codec
        {
            private readonly TestPerFieldPostingsFormat2 OuterInstance;

            public Lucene46CodecAnonymousInnerClassHelper2(TestPerFieldPostingsFormat2 outerInstance)
            {
                this.OuterInstance = outerInstance;
            }

            public override PostingsFormat GetPostingsFormatForField(string field)
            {
              if ("id".Equals(field))
              {
                return new Pulsing41PostingsFormat(1);
              }
              else if ("date".Equals(field))
              {
                return new Pulsing41PostingsFormat(2);
              }
              else
              {
                return base.GetPostingsFormatForField(field);
              }
            }
        }*/

        [Test]
        private void TestMixedPostings(Codec codec)
        {
            Directory dir = NewDirectory();
            IndexWriterConfig iwc = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
            iwc.SetCodec(codec);
            RandomIndexWriter iw = new RandomIndexWriter(Random(), dir, iwc);
            Document doc = new Document();
            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            // turn on vectors for the checkindex cross-check
            ft.StoreTermVectors = true;
            ft.StoreTermVectorOffsets = true;
            ft.StoreTermVectorPositions = true;
            Field idField = new Field("id", "", ft);
            Field dateField = new Field("date", "", ft);
            doc.Add(idField);
            doc.Add(dateField);
            for (int i = 0; i < 100; i++)
            {
                idField.StringValue = Convert.ToString(Random().Next(50));
                dateField.StringValue = Convert.ToString(Random().Next(100));
                iw.AddDocument(doc);
            }
            iw.Dispose();
            dir.Dispose(); // checkindex
        }
    }
}