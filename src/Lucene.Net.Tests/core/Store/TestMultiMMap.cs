using NUnit.Framework;
using System;
using System.IO;

namespace Lucene.Net.Store
{
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using IndexInputSlicer = Lucene.Net.Store.Directory.IndexInputSlicer;
    using IndexReader = Lucene.Net.Index.IndexReader;
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

    /// <summary>
    /// Tests MMapDirectory's MultiMMapIndexInput
    /// <p>
    /// Because Java's ByteBuffer uses an int to address the
    /// values, it's necessary to access a file >
    /// Integer.MAX_VALUE in size using multiple byte buffers.
    /// </summary>
    [TestFixture]
    public class TestMultiMMap : LuceneTestCase
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            AssumeTrue("test requires a jre that supports unmapping", MMapDirectory.UNMAP_SUPPORTED);
        }

        [Test]
        public virtual void TestCloneSafety()
        {
            MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSafety"));
            IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
            io.WriteVInt(5);
            io.Dispose();
            IndexInput one = mmapDir.OpenInput("bytes", IOContext.DEFAULT);
            IndexInput two = (IndexInput)one.Clone();
            IndexInput three = (IndexInput)two.Clone(); // clone of clone
            one.Dispose();
            try
            {
                one.ReadVInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            try
            {
                two.ReadVInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            try
            {
                three.ReadVInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            two.Dispose();
            three.Dispose();
            // test double close of master:
            one.Dispose();
            mmapDir.Dispose();
        }

        [Test]
        public virtual void TestCloneClose()
        {
            MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneClose"));
            IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
            io.WriteVInt(5);
            io.Dispose();
            IndexInput one = mmapDir.OpenInput("bytes", IOContext.DEFAULT);
            IndexInput two = (IndexInput)one.Clone();
            IndexInput three = (IndexInput)two.Clone(); // clone of clone
            two.Dispose();
            Assert.AreEqual(5, one.ReadVInt());
            try
            {
                two.ReadVInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            Assert.AreEqual(5, three.ReadVInt());
            one.Dispose();
            three.Dispose();
            mmapDir.Dispose();
        }

        [Test]
        public virtual void TestCloneSliceSafety()
        {
            MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSliceSafety"));
            IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
            io.WriteInt(1);
            io.WriteInt(2);
            io.Dispose();
            IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random()));
            IndexInput one = slicer.OpenSlice("first int", 0, 4);
            IndexInput two = slicer.OpenSlice("second int", 4, 4);
            IndexInput three = (IndexInput)one.Clone(); // clone of clone
            IndexInput four = (IndexInput)two.Clone(); // clone of clone
            slicer.Dispose();
            try
            {
                one.ReadInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            try
            {
                two.ReadInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            try
            {
                three.ReadInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            try
            {
                four.ReadInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            one.Dispose();
            two.Dispose();
            three.Dispose();
            four.Dispose();
            // test double-close of slicer:
            slicer.Dispose();
            mmapDir.Dispose();
        }

        [Test]
        public virtual void TestCloneSliceClose()
        {
            MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testCloneSliceClose"));
            IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
            io.WriteInt(1);
            io.WriteInt(2);
            io.Dispose();
            IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random()));
            IndexInput one = slicer.OpenSlice("first int", 0, 4);
            IndexInput two = slicer.OpenSlice("second int", 4, 4);
            one.Dispose();
            try
            {
                one.ReadInt();
                Assert.Fail("Must throw AlreadyClosedException");
            }
            catch (AlreadyClosedException ignore)
            {
                // pass
            }
            Assert.AreEqual(2, two.ReadInt());
            // reopen a new slice "one":
            one = slicer.OpenSlice("first int", 0, 4);
            Assert.AreEqual(1, one.ReadInt());
            one.Dispose();
            two.Dispose();
            slicer.Dispose();
            mmapDir.Dispose();
        }

        [Test]
        public virtual void TestSeekZero()
        {
            for (int i = 0; i < 31; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekZero"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("zeroBytes", NewIOContext(Random()));
                io.Dispose();
                IndexInput ii = mmapDir.OpenInput("zeroBytes", NewIOContext(Random()));
                ii.Seek(0L);
                ii.Dispose();
                mmapDir.Dispose();
            }
        }

        [Test]
        public virtual void TestSeekSliceZero()
        {
            for (int i = 0; i < 31; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekSliceZero"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("zeroBytes", NewIOContext(Random()));
                io.Dispose();
                IndexInputSlicer slicer = mmapDir.CreateSlicer("zeroBytes", NewIOContext(Random()));
                IndexInput ii = slicer.OpenSlice("zero-length slice", 0, 0);
                ii.Seek(0L);
                ii.Dispose();
                slicer.Dispose();
                mmapDir.Dispose();
            }
        }

        [Test]
        public virtual void TestSeekEnd()
        {
            for (int i = 0; i < 17; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekEnd"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
                sbyte[] bytes = new sbyte[1 << i];
                Random().NextBytes((byte[])(Array)bytes);
                io.WriteBytes(bytes, bytes.Length);
                io.Dispose();
                IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
                sbyte[] actual = new sbyte[1 << i];
                ii.ReadBytes(actual, 0, actual.Length);
                Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
                ii.Seek(1 << i);
                ii.Dispose();
                mmapDir.Dispose();
            }
        }

        [Test]
        public virtual void TestSeekSliceEnd()
        {
            for (int i = 0; i < 17; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeekSliceEnd"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
                sbyte[] bytes = new sbyte[1 << i];
                Random().NextBytes((byte[])(Array)bytes);
                io.WriteBytes(bytes, bytes.Length);
                io.Dispose();
                IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random()));
                IndexInput ii = slicer.OpenSlice("full slice", 0, bytes.Length);
                sbyte[] actual = new sbyte[1 << i];
                ii.ReadBytes(actual, 0, actual.Length);
                Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
                ii.Seek(1 << i);
                ii.Dispose();
                slicer.Dispose();
                mmapDir.Dispose();
            }
        }

        [Test]
        public virtual void TestSeeking()
        {
            for (int i = 0; i < 10; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSeeking"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
                sbyte[] bytes = new sbyte[1 << (i + 1)]; // make sure we switch buffers
                Random().NextBytes((byte[])(Array)bytes);
                io.WriteBytes(bytes, bytes.Length);
                io.Dispose();
                IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
                sbyte[] actual = new sbyte[1 << (i + 1)]; // first read all bytes
                ii.ReadBytes(actual, 0, actual.Length);
                Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
                for (int sliceStart = 0; sliceStart < bytes.Length; sliceStart++)
                {
                    for (int sliceLength = 0; sliceLength < bytes.Length - sliceStart; sliceLength++)
                    {
                        sbyte[] slice = new sbyte[sliceLength];
                        ii.Seek(sliceStart);
                        ii.ReadBytes(slice, 0, slice.Length);
                        Assert.AreEqual(new BytesRef(bytes, sliceStart, sliceLength), new BytesRef(slice));
                    }
                }
                ii.Dispose();
                mmapDir.Dispose();
            }
        }

        // note instead of seeking to offset and reading length, this opens slices at the
        // the various offset+length and just does readBytes.
        [Test]
        public virtual void TestSlicedSeeking()
        {
            for (int i = 0; i < 10; i++)
            {
                MMapDirectory mmapDir = new MMapDirectory(CreateTempDir("testSlicedSeeking"), null, 1 << i);
                IndexOutput io = mmapDir.CreateOutput("bytes", NewIOContext(Random()));
                sbyte[] bytes = new sbyte[1 << (i + 1)]; // make sure we switch buffers
                Random().NextBytes((byte[])(Array)bytes);
                io.WriteBytes(bytes, bytes.Length);
                io.Dispose();
                IndexInput ii = mmapDir.OpenInput("bytes", NewIOContext(Random()));
                sbyte[] actual = new sbyte[1 << (i + 1)]; // first read all bytes
                ii.ReadBytes(actual, 0, actual.Length);
                ii.Dispose();
                Assert.AreEqual(new BytesRef(bytes), new BytesRef(actual));
                IndexInputSlicer slicer = mmapDir.CreateSlicer("bytes", NewIOContext(Random()));
                for (int sliceStart = 0; sliceStart < bytes.Length; sliceStart++)
                {
                    for (int sliceLength = 0; sliceLength < bytes.Length - sliceStart; sliceLength++)
                    {
                        sbyte[] slice = new sbyte[sliceLength];
                        IndexInput input = slicer.OpenSlice("bytesSlice", sliceStart, slice.Length);
                        input.ReadBytes(slice, 0, slice.Length);
                        input.Dispose();
                        Assert.AreEqual(new BytesRef(bytes, sliceStart, sliceLength), new BytesRef(slice));
                    }
                }
                slicer.Dispose();
                mmapDir.Dispose();
            }
        }

        [Test]
        public virtual void TestRandomChunkSizes()
        {
            int num = AtLeast(10);
            for (int i = 0; i < num; i++)
            {
                AssertChunking(Random(), TestUtil.NextInt(Random(), 20, 100));
            }
        }

        private void AssertChunking(Random random, int chunkSize)
        {
            DirectoryInfo path = CreateTempDir("mmap" + chunkSize);
            MMapDirectory mmapDir = new MMapDirectory(path, null, chunkSize);
            // we will map a lot, try to turn on the unmap hack
            if (MMapDirectory.UNMAP_SUPPORTED)
            {
                mmapDir.UseUnmap = true;
            }
            MockDirectoryWrapper dir = new MockDirectoryWrapper(random, mmapDir);
            RandomIndexWriter writer = new RandomIndexWriter(random, dir, NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(random)).SetMergePolicy(NewLogMergePolicy()));
            Document doc = new Document();
            Field docid = NewStringField("docid", "0", Field.Store.YES);
            Field junk = NewStringField("junk", "", Field.Store.YES);
            doc.Add(docid);
            doc.Add(junk);

            int numDocs = 100;
            for (int i = 0; i < numDocs; i++)
            {
                docid.StringValue = "" + i;
                junk.StringValue = TestUtil.RandomUnicodeString(random);
                writer.AddDocument(doc);
            }
            IndexReader reader = writer.Reader;
            writer.Dispose();

            int numAsserts = AtLeast(100);
            for (int i = 0; i < numAsserts; i++)
            {
                int docID = random.Next(numDocs);
                Assert.AreEqual("" + docID, reader.Document(docID).Get("docid"));
            }
            reader.Dispose();
            dir.Dispose();
        }
    }
}