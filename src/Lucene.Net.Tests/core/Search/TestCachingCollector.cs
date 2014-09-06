namespace Lucene.Net.Search
{
    using NUnit.Framework;
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

    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

    [TestFixture]
    public class TestCachingCollector : LuceneTestCase
    {
        private const double ONE_BYTE = 1.0 / (1024 * 1024); // 1 byte out of MB

        private class MockScorer : Scorer
        {
            internal MockScorer()
                : base((Weight)null)
            {
            }

            public override float Score()
            {
                return 0;
            }

            public override int Freq()
            {
                return 0;
            }

            public override int DocID()
            {
                return 0;
            }

            public override int NextDoc()
            {
                return 0;
            }

            public override int Advance(int target)
            {
                return 0;
            }

            public override long Cost()
            {
                return 1;
            }
        }

        private class NoOpCollector : Collector
        {
            internal readonly bool AcceptDocsOutOfOrder;

            public NoOpCollector(bool acceptDocsOutOfOrder)
            {
                this.AcceptDocsOutOfOrder = acceptDocsOutOfOrder;
            }

            public override Scorer Scorer
            {
                set
                {
                }
            }

            public override void Collect(int doc)
            {
            }

            public override AtomicReaderContext NextReader
            {
                set
                {
                }
            }

            public override bool AcceptsDocsOutOfOrder()
            {
                return AcceptDocsOutOfOrder;
            }
        }

        [Test]
        public virtual void TestBasic()
        {
            foreach (bool cacheScores in new bool[] { false, true })
            {
                CachingCollector cc = CachingCollector.Create(new NoOpCollector(false), cacheScores, 1.0);
                cc.Scorer = new MockScorer();

                // collect 1000 docs
                for (int i = 0; i < 1000; i++)
                {
                    cc.Collect(i);
                }

                // now replay them
                cc.Replay(new CollectorAnonymousInnerClassHelper(this));
            }
        }

        private class CollectorAnonymousInnerClassHelper : Collector
        {
            private readonly TestCachingCollector OuterInstance;

            public CollectorAnonymousInnerClassHelper(TestCachingCollector outerInstance)
            {
                this.OuterInstance = outerInstance;
                prevDocID = -1;
            }

            internal int prevDocID;

            public override Scorer Scorer
            {
                set
                {
                }
            }

            public override AtomicReaderContext NextReader
            {
                set
                {
                }
            }

            public override void Collect(int doc)
            {
                Assert.AreEqual(prevDocID + 1, doc);
                prevDocID = doc;
            }

            public override bool AcceptsDocsOutOfOrder()
            {
                return false;
            }
        }

        [Test]
        public virtual void TestIllegalStateOnReplay()
        {
            CachingCollector cc = CachingCollector.Create(new NoOpCollector(false), true, 50 * ONE_BYTE);
            cc.Scorer = new MockScorer();

            // collect 130 docs, this should be enough for triggering cache abort.
            for (int i = 0; i < 130; i++)
            {
                cc.Collect(i);
            }

            Assert.IsFalse(cc.Cached, "CachingCollector should not be cached due to low memory limit");

            try
            {
                cc.Replay(new NoOpCollector(false));
                Assert.Fail("replay should fail if CachingCollector is not cached");
            }
            catch (InvalidOperationException e)
            {
                // expected
            }
        }

        [Test]
        public virtual void TestIllegalCollectorOnReplay()
        {
            // tests that the Collector passed to replay() has an out-of-order mode that
            // is valid with the Collector passed to the ctor

            // 'src' Collector does not support out-of-order
            CachingCollector cc = CachingCollector.Create(new NoOpCollector(false), true, 50 * ONE_BYTE);
            cc.Scorer = new MockScorer();
            for (int i = 0; i < 10; i++)
            {
                cc.Collect(i);
            }
            cc.Replay(new NoOpCollector(true)); // this call should not fail
            cc.Replay(new NoOpCollector(false)); // this call should not fail

            // 'src' Collector supports out-of-order
            cc = CachingCollector.Create(new NoOpCollector(true), true, 50 * ONE_BYTE);
            cc.Scorer = new MockScorer();
            for (int i = 0; i < 10; i++)
            {
                cc.Collect(i);
            }
            cc.Replay(new NoOpCollector(true)); // this call should not fail
            try
            {
                cc.Replay(new NoOpCollector(false)); // this call should fail
                Assert.Fail("should have failed if an in-order Collector was given to replay(), " + "while CachingCollector was initialized with out-of-order collection");
            }
            catch (System.ArgumentException e)
            {
                // ok
            }
        }

        [Test]
        public virtual void TestCachedArraysAllocation()
        {
            // tests the cached arrays allocation -- if the 'nextLength' was too high,
            // caching would terminate even if a smaller length would suffice.

            // set RAM limit enough for 150 docs + random(10000)
            int numDocs = Random().Next(10000) + 150;
            foreach (bool cacheScores in new bool[] { false, true })
            {
                int bytesPerDoc = cacheScores ? 8 : 4;
                CachingCollector cc = CachingCollector.Create(new NoOpCollector(false), cacheScores, bytesPerDoc * ONE_BYTE * numDocs);
                cc.Scorer = new MockScorer();
                for (int i = 0; i < numDocs; i++)
                {
                    cc.Collect(i);
                }
                Assert.IsTrue(cc.Cached);

                // The 151's document should terminate caching
                cc.Collect(numDocs);
                Assert.IsFalse(cc.Cached);
            }
        }

        [Test]
        public virtual void TestNoWrappedCollector()
        {
            foreach (bool cacheScores in new bool[] { false, true })
            {
                // create w/ null wrapped collector, and test that the methods work
                CachingCollector cc = CachingCollector.Create(true, cacheScores, 50 * ONE_BYTE);
                cc.NextReader = null;
                cc.Scorer = new MockScorer();
                cc.Collect(0);

                Assert.IsTrue(cc.Cached);
                cc.Replay(new NoOpCollector(true));
            }
        }
    }
}