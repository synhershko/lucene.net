using Lucene.Net.Support;
using System.Text;

namespace Lucene.Net.Document
{
    using NUnit.Framework;
    using System;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Directory = Lucene.Net.Store.Directory;
    using IndexableField = Lucene.Net.Index.IndexableField;
    using IndexReader = Lucene.Net.Index.IndexReader;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using RandomIndexWriter = Lucene.Net.Index.RandomIndexWriter;

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

    /// <summary>
    /// Tests <seealso cref="Document"/> class.
    /// </summary>
    [TestFixture]
    public class TestBinaryDocument : LuceneTestCase
    {
        internal string BinaryValStored = "this text will be stored as a byte array in the index";
        internal string BinaryValCompressed = "this text will be also stored and compressed as a byte array in the index";

        [Test]
        public virtual void TestBinaryFieldInIndex()
        {
            FieldType ft = new FieldType();
            ft.Stored = true;
            IndexableField binaryFldStored = new StoredField("binaryStored", (sbyte[])(Array)System.Text.UTF8Encoding.UTF8.GetBytes(BinaryValStored));
            IndexableField stringFldStored = new Field("stringStored", BinaryValStored, ft);

            Document doc = new Document();

            doc.Add(binaryFldStored);

            doc.Add(stringFldStored);

            /// <summary>
            /// test for field count </summary>
            Assert.AreEqual(2, doc.Fields.Count);

            /// <summary>
            /// add the doc to a ram index </summary>
            Directory dir = NewDirectory();
            Random r = new Random();
            RandomIndexWriter writer = new RandomIndexWriter(r, dir);
            writer.AddDocument(doc);

            /// <summary>
            /// open a reader and fetch the document </summary>
            IndexReader reader = writer.Reader;
            Document docFromReader = reader.Document(0);
            Assert.IsTrue(docFromReader != null);

            /// <summary>
            /// fetch the binary stored field and compare it's content with the original one </summary>
            BytesRef bytes = docFromReader.GetBinaryValue("binaryStored");
            Assert.IsNotNull(bytes);

            string binaryFldStoredTest = Encoding.UTF8.GetString((byte[])(Array)bytes.Bytes).Substring(bytes.Offset, bytes.Length);
            //new string(bytes.Bytes, bytes.Offset, bytes.Length, IOUtils.CHARSET_UTF_8);
            Assert.IsTrue(binaryFldStoredTest.Equals(BinaryValStored));

            /// <summary>
            /// fetch the string field and compare it's content with the original one </summary>
            string stringFldStoredTest = docFromReader.Get("stringStored");
            Assert.IsTrue(stringFldStoredTest.Equals(BinaryValStored));

            writer.Dispose();
            reader.Dispose();
            dir.Dispose();
        }

        [Test]
        public virtual void TestCompressionTools()
        {
            IndexableField binaryFldCompressed = new StoredField("binaryCompressed", (sbyte[])(Array)CompressionTools.Compress(BinaryValCompressed.GetBytes(Encoding.UTF8)));
            IndexableField stringFldCompressed = new StoredField("stringCompressed", (sbyte[])(Array)CompressionTools.CompressString(BinaryValCompressed));

            var doc = new Document {binaryFldCompressed, stringFldCompressed};

            using (Directory dir = NewDirectory())
            using (RandomIndexWriter writer = new RandomIndexWriter(Random(), dir))
            {
                writer.AddDocument(doc);

                using (IndexReader reader = writer.Reader)
                {
                    Document docFromReader = reader.Document(0);
                    Assert.IsTrue(docFromReader != null);

                    string binaryFldCompressedTest =
                        Encoding.UTF8.GetString(
                            CompressionTools.Decompress(docFromReader.GetBinaryValue("binaryCompressed")));
                    //new string(CompressionTools.Decompress(docFromReader.GetBinaryValue("binaryCompressed")), IOUtils.CHARSET_UTF_8);
                    Assert.IsTrue(binaryFldCompressedTest.Equals(BinaryValCompressed));
                    Assert.IsTrue(
                        CompressionTools.DecompressString(docFromReader.GetBinaryValue("stringCompressed"))
                            .Equals(BinaryValCompressed));
                }

            }
        }
    }
}