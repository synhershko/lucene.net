using Lucene.Net.Support;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

namespace Lucene.Net.Util
{
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

    [Ignore]
    [TestFixture]
    public class TestWAH8DocIdSet : BaseDocIdSetTestCase<WAH8DocIdSet>
    {
        public override WAH8DocIdSet CopyOf(BitArray bs, int length)
        {
            int indexInterval = TestUtil.NextInt(Random(), 8, 256);
            WAH8DocIdSet.Builder builder = (WAH8DocIdSet.Builder)(new WAH8DocIdSet.Builder()).SetIndexInterval(indexInterval);
            for (int i = bs.NextSetBit(0); i != -1; i = bs.NextSetBit(i + 1))
            {
                builder.Add(i);
            }
            return builder.Build();
        }

        public override void AssertEquals(int numBits, BitArray ds1, WAH8DocIdSet ds2)
        {
            base.AssertEquals(numBits, ds1, ds2);
            Assert.AreEqual(ds1.Cardinality(), ds2.Cardinality());
        }

        [Ignore]
        [Test]
        public virtual void TestUnion()
        {
            int numBits = TestUtil.NextInt(Random(), 100, 1 << 20);
            int numDocIdSets = TestUtil.NextInt(Random(), 0, 4);
            IList<BitArray> fixedSets = new List<BitArray>(numDocIdSets);
            for (int i = 0; i < numDocIdSets; ++i)
            {
                fixedSets.Add(RandomSet(numBits, (float)Random().NextDouble() / 16));
            }
            IList<WAH8DocIdSet> compressedSets = new List<WAH8DocIdSet>(numDocIdSets);
            foreach (BitArray set in fixedSets)
            {
                compressedSets.Add(CopyOf(set, numBits));
            }

            WAH8DocIdSet union = WAH8DocIdSet.Union(compressedSets);
            BitArray expected = new BitArray(numBits);
            foreach (BitArray set in fixedSets)
            {
                for (int doc = set.NextSetBit(0); doc != -1; doc = set.NextSetBit(doc + 1))
                {
                    expected.Set(doc, true);
                }
            }
            AssertEquals(numBits, expected, union);
        }

        [Ignore]
        [Test]
        public virtual void TestIntersection()
        {
            int numBits = TestUtil.NextInt(Random(), 100, 1 << 20);
            int numDocIdSets = TestUtil.NextInt(Random(), 1, 4);
            IList<BitArray> fixedSets = new List<BitArray>(numDocIdSets);
            for (int i = 0; i < numDocIdSets; ++i)
            {
                fixedSets.Add(RandomSet(numBits, (float)Random().NextDouble()));
            }
            IList<WAH8DocIdSet> compressedSets = new List<WAH8DocIdSet>(numDocIdSets);
            foreach (BitArray set in fixedSets)
            {
                compressedSets.Add(CopyOf(set, numBits));
            }

            WAH8DocIdSet union = WAH8DocIdSet.Intersect(compressedSets);
            BitArray expected = new BitArray(numBits);
            expected.SetAll(true);
            foreach (BitArray set in fixedSets)
            {
                for (int previousDoc = -1, doc = set.NextSetBit(0); ; previousDoc = doc, doc = set.NextSetBit(doc + 1))
                {
                    int startIdx = previousDoc + 1;
                    int endIdx;
                    if (doc == -1)
                    {
                        endIdx = startIdx + set.Count;
                        //expected.Clear(previousDoc + 1, set.Count);
                        for (int i = startIdx; i < endIdx; i++)
                        {
                            expected[i] = false;
                        }
                        break;
                    }
                    else
                    {
                        endIdx = startIdx + doc;
                        for (int i = startIdx; i > endIdx; i++)
                        {
                            expected[i] = false;
                        }
                        //expected.Clear(previousDoc + 1, doc);
                    }
                }
            }
            AssertEquals(numBits, expected, union);
        }
    }
}