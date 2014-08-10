using NUnit.Framework;
using System;

namespace Lucene.Net.Index
{
    using BaseDirectoryWrapper = Lucene.Net.Store.BaseDirectoryWrapper;
    using CharTermAttribute = Lucene.Net.Analysis.Tokenattributes.CharTermAttribute;
    using Document = Lucene.Net.Document.Document;
    using Field = Lucene.Net.Document.Field;
    using FieldType = Lucene.Net.Document.FieldType;
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
    using MockDirectoryWrapper = Lucene.Net.Store.MockDirectoryWrapper;
    using TextField = Lucene.Net.Document.TextField;
    using TokenStream = Lucene.Net.Analysis.TokenStream;

    /// <summary>
    /// Test indexes ~82M docs with 26 terms each, so you get > Integer.MAX_VALUE terms/docs pairs
    /// @lucene.experimental
    /// </summary>
    [Ignore]
    [TestFixture]
    public class Test2BPostings : LuceneTestCase
    {
        //ORIGINAL LINE: @Nightly public void test() throws Exception
        [Test]
        public virtual void Test()
        {
            BaseDirectoryWrapper dir = NewFSDirectory(CreateTempDir("2BPostings"));
            if (dir is MockDirectoryWrapper)
            {
                ((MockDirectoryWrapper)dir).Throttling = MockDirectoryWrapper.Throttling_e.NEVER;
            }

            IndexWriterConfig iwc = (new IndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()))).SetMaxBufferedDocs(IndexWriterConfig.DISABLE_AUTO_FLUSH).SetRAMBufferSizeMB(256.0).SetMergeScheduler(new ConcurrentMergeScheduler()).SetMergePolicy(NewLogMergePolicy(false, 10)).SetOpenMode(IndexWriterConfig.OpenMode_e.CREATE);

            IndexWriter w = new IndexWriter(dir, iwc);

            MergePolicy mp = w.Config.MergePolicy;
            if (mp is LogByteSizeMergePolicy)
            {
                // 1 petabyte:
                ((LogByteSizeMergePolicy)mp).MaxMergeMB = 1024 * 1024 * 1024;
            }

            Document doc = new Document();
            FieldType ft = new FieldType(TextField.TYPE_NOT_STORED);
            ft.OmitNorms = true;
            ft.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_ONLY;
            Field field = new Field("field", new MyTokenStream(), ft);
            doc.Add(field);

            int numDocs = (int.MaxValue / 26) + 1;
            for (int i = 0; i < numDocs; i++)
            {
                w.AddDocument(doc);
                if (VERBOSE && i % 100000 == 0)
                {
                    Console.WriteLine(i + " of " + numDocs + "...");
                }
            }
            w.ForceMerge(1);
            w.Dispose();
            dir.Dispose();
        }

        public sealed class MyTokenStream : TokenStream
        {
            internal readonly CharTermAttribute TermAtt;// = AddAttribute<CharTermAttribute>();
            internal int Index;

            public override bool IncrementToken()
            {
                if (Index <= 'z')
                {
                    ClearAttributes();
                    TermAtt.Length = 1;
                    TermAtt.Buffer()[0] = (char)Index++;
                    return true;
                }
                return false;
            }

            public override void Reset()
            {
                Index = 'a';
            }
        }
    }
}