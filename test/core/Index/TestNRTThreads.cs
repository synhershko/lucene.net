using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Lucene.Net.Index
{
    using Lucene.Net.Randomized.Generators;
    using NUnit.Framework;
    using System.Collections.Generic;
    using Directory = Lucene.Net.Store.Directory;

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

    using IndexSearcher = Lucene.Net.Search.IndexSearcher;
    using MockDirectoryWrapper = Lucene.Net.Store.MockDirectoryWrapper;

    // TODO
    //   - mix in forceMerge, addIndexes
    //   - randomoly mix in non-congruent docs

    [TestFixture]
    public class TestNRTThreads : ThreadedIndexingAndSearchingTestCase
    {
        private bool UseNonNrtReaders = true;

        [SetUp]
        public virtual void SetUp()
        {
            base.SetUp();
            UseNonNrtReaders = Random().NextBoolean();
        }

        protected override void DoSearching(TaskScheduler es, long stopTime)
        {
            bool anyOpenDelFiles = false;

            DirectoryReader r = DirectoryReader.Open(Writer, true);

            while (DateTime.Now.Millisecond < stopTime && !Failed.Get())
            {
                if (Random().NextBoolean())
                {
                    if (VERBOSE)
                    {
                        Console.WriteLine("TEST: now reopen r=" + r);
                    }
                    DirectoryReader r2 = DirectoryReader.OpenIfChanged(r);
                    if (r2 != null)
                    {
                        r.Dispose();
                        r = r2;
                    }
                }
                else
                {
                    if (VERBOSE)
                    {
                        Console.WriteLine("TEST: now close reader=" + r);
                    }
                    r.Dispose();
                    Writer.Commit();
                    ISet<string> openDeletedFiles = ((MockDirectoryWrapper)Dir).OpenDeletedFiles;
                    if (openDeletedFiles.Count > 0)
                    {
                        Console.WriteLine("OBD files: " + openDeletedFiles);
                    }
                    anyOpenDelFiles |= openDeletedFiles.Count > 0;
                    //Assert.AreEqual("open but deleted: " + openDeletedFiles, 0, openDeletedFiles.Size());
                    if (VERBOSE)
                    {
                        Console.WriteLine("TEST: now open");
                    }
                    r = DirectoryReader.Open(Writer, true);
                }
                if (VERBOSE)
                {
                    Console.WriteLine("TEST: got new reader=" + r);
                }
                //System.out.println("numDocs=" + r.NumDocs() + "
                //openDelFileCount=" + dir.openDeleteFileCount());

                if (r.NumDocs() > 0)
                {
                    FixedSearcher = new IndexSearcher(r, es);
                    SmokeTestSearcher(FixedSearcher);
                    RunSearchThreads(DateTime.Now.Millisecond + 500);
                }
            }
            r.Dispose();

            //System.out.println("numDocs=" + r.NumDocs() + " openDelFileCount=" + dir.openDeleteFileCount());
            ISet<string> openDeletedFiles_ = ((MockDirectoryWrapper)Dir).OpenDeletedFiles;
            if (openDeletedFiles_.Count > 0)
            {
                Console.WriteLine("OBD files: " + openDeletedFiles_);
            }
            anyOpenDelFiles |= openDeletedFiles_.Count > 0;

            Assert.IsFalse(anyOpenDelFiles, "saw non-zero open-but-deleted count");
        }

        protected override Directory GetDirectory(Directory @in)
        {
            Debug.Assert(@in is MockDirectoryWrapper);
            if (!UseNonNrtReaders)
            {
                ((MockDirectoryWrapper)@in).AssertNoDeleteOpenFile = true;
            }
            return @in;
        }

        protected override void DoAfterWriter(TaskScheduler es)
        {
            // Force writer to do reader pooling, always, so that
            // all merged segments, even for merges before
            // doSearching is called, are warmed:
            Writer.Reader.Dispose();
        }

        private IndexSearcher FixedSearcher;

        protected override IndexSearcher CurrentSearcher
        {
            get
            {
                return FixedSearcher;
            }
        }

        protected override void ReleaseSearcher(IndexSearcher s)
        {
            if (s != FixedSearcher)
            {
                // Final searcher:
                s.IndexReader.Dispose();
            }
        }

        protected override IndexSearcher FinalSearcher
        {
            get
            {
                IndexReader r2;
                if (UseNonNrtReaders)
                {
                    if (Random().NextBoolean())
                    {
                        r2 = Writer.Reader;
                    }
                    else
                    {
                        Writer.Commit();
                        r2 = DirectoryReader.Open(Dir);
                    }
                }
                else
                {
                    r2 = Writer.Reader;
                }
                return NewSearcher(r2);
            }
        }

        [Test]
        public virtual void TestNRTThreads_Mem()
        {
            RunTest("TestNRTThreads");
        }
    }
}