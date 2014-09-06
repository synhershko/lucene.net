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

using Lucene.Net.Randomized.Generators;
using Lucene.Net.Support;
using NUnit.Framework;
using System.Collections;

namespace Lucene.Net.Util
{
    using DocIdSetIterator = Lucene.Net.Search.DocIdSetIterator;

    [TestFixture]
    public class TestOpenBitSet : BaseDocIdSetTestCase<OpenBitSet>
    {
        public override OpenBitSet CopyOf(BitArray bs, int length)
        {
            OpenBitSet set = new OpenBitSet(length);
            for (int doc = bs.NextSetBit(0); doc != -1; doc = bs.NextSetBit(doc + 1))
            {
                set.Set(doc);
            }
            return set;
        }

        internal virtual void DoGet(BitArray a, OpenBitSet b)
        {
            int max = a.Count;
            for (int i = 0; i < max; i++)
            {
                if (a.Get(i) != b.Get(i))
                {
                    Assert.Fail("mismatch: BitSet=[" + i + "]=" + a.Get(i));
                }
                if (a.Get(i) != b.Get((long)i))
                {
                    Assert.Fail("mismatch: BitSet=[" + i + "]=" + a.Get(i));
                }
            }
        }

        internal virtual void DoGetFast(BitArray a, OpenBitSet b, int max)
        {
            for (int i = 0; i < max; i++)
            {
                if (a.Get(i) != b.FastGet(i))
                {
                    Assert.Fail("mismatch: BitSet=[" + i + "]=" + a.Get(i));
                }
                if (a.Get(i) != b.FastGet((long)i))
                {
                    Assert.Fail("mismatch: BitSet=[" + i + "]=" + a.Get(i));
                }
            }
        }

        internal virtual void DoNextSetBit(BitArray a, OpenBitSet b)
        {
            int aa = -1, bb = -1;
            do
            {
                aa = a.NextSetBit(aa + 1);
                bb = b.NextSetBit(bb + 1);
                Assert.AreEqual(aa, bb);
            } while (aa >= 0);
        }

        internal virtual void DoNextSetBitLong(BitArray a, OpenBitSet b)
        {
            int aa = -1, bb = -1;
            do
            {
                aa = a.NextSetBit(aa + 1);
                bb = (int)b.NextSetBit((long)(bb + 1));
                Assert.AreEqual(aa, bb);
            } while (aa >= 0);
        }

        internal virtual void DoPrevSetBit(BitArray a, OpenBitSet b)
        {
            int aa = a.Count + Random().Next(100);
            int bb = aa;
            do
            {
                // aa = a.PrevSetBit(aa-1);
                aa--;
                while ((aa >= 0) && (!a.Get(aa)))
                {
                    aa--;
                }
                bb = b.PrevSetBit(bb - 1);
                Assert.AreEqual(aa, bb);
            } while (aa >= 0);
        }

        internal virtual void DoPrevSetBitLong(BitArray a, OpenBitSet b)
        {
            int aa = a.Count + Random().Next(100);
            int bb = aa;
            do
            {
                // aa = a.PrevSetBit(aa-1);
                aa--;
                while ((aa >= 0) && (!a.Get(aa)))
                {
                    aa--;
                }
                bb = (int)b.PrevSetBit((long)(bb - 1));
                Assert.AreEqual(aa, bb);
            } while (aa >= 0);
        }

        // test interleaving different OpenBitSetIterator.Next()/skipTo()
        internal virtual void DoIterate(BitArray a, OpenBitSet b, int mode)
        {
            if (mode == 1)
            {
                DoIterate1(a, b);
            }
            if (mode == 2)
            {
                DoIterate2(a, b);
            }
        }

        internal virtual void DoIterate1(BitArray a, OpenBitSet b)
        {
            int aa = -1, bb = -1;
            OpenBitSetIterator iterator = new OpenBitSetIterator(b);
            do
            {
                aa = a.NextSetBit(aa + 1);
                bb = Random().NextBoolean() ? iterator.NextDoc() : iterator.Advance(bb + 1);
                Assert.AreEqual(aa == -1 ? DocIdSetIterator.NO_MORE_DOCS : aa, bb);
            } while (aa >= 0);
        }

        internal virtual void DoIterate2(BitArray a, OpenBitSet b)
        {
            int aa = -1, bb = -1;
            OpenBitSetIterator iterator = new OpenBitSetIterator(b);
            do
            {
                aa = a.NextSetBit(aa + 1);
                bb = Random().NextBoolean() ? iterator.NextDoc() : iterator.Advance(bb + 1);
                Assert.AreEqual(aa == -1 ? DocIdSetIterator.NO_MORE_DOCS : aa, bb);
            } while (aa >= 0);
        }

        internal virtual void DoRandomSets(int maxSize, int iter, int mode)
        {
            BitArray a0 = null;
            OpenBitSet b0 = null;

            for (int i = 0; i < iter; i++)
            {
                int sz = Random().Next(maxSize);
                BitArray a = new BitArray(sz);
                OpenBitSet b = new OpenBitSet(sz);

                // test the various ways of setting bits
                if (sz > 0)
                {
                    int nOper = Random().Next(sz);
                    for (int j = 0; j < nOper; j++)
                    {
                        int idx;

                        idx = Random().Next(sz);
                        a.Set(idx, true);
                        b.FastSet(idx);

                        idx = Random().Next(sz);
                        a.Set(idx, true);
                        b.FastSet((long)idx);

                        idx = Random().Next(sz);
                        a.Set(idx, false);
                        b.FastClear(idx);

                        idx = Random().Next(sz);
                        a.Set(idx, false);
                        b.FastClear((long)idx);

                        idx = Random().Next(sz);
                        a.Set(idx, !a.Get(idx));
                        b.FastFlip(idx);

                        bool val = b.FlipAndGet(idx);
                        bool val2 = b.FlipAndGet(idx);
                        Assert.IsTrue(val != val2);

                        idx = Random().Next(sz);
                        a.Set(idx, !a.Get(idx));
                        b.FastFlip((long)idx);

                        val = b.FlipAndGet((long)idx);
                        val2 = b.FlipAndGet((long)idx);
                        Assert.IsTrue(val != val2);

                        val = b.GetAndSet(idx);
                        Assert.IsTrue(val2 == val);
                        Assert.IsTrue(b.Get(idx));

                        if (!val)
                        {
                            b.FastClear(idx);
                        }
                        Assert.IsTrue(b.Get(idx) == val);
                    }
                }

                // test that the various ways of accessing the bits are equivalent
                DoGet(a, b);
                DoGetFast(a, b, sz);

                // test ranges, including possible extension
                int fromIndex, toIndex;
                fromIndex = Random().Next(sz + 80);
                toIndex = fromIndex + Random().Next((sz >> 1) + 1);
                BitArray aa = (BitArray)a.Clone();
                aa.Flip(fromIndex, toIndex);
                OpenBitSet bb = (OpenBitSet)b.Clone();
                bb.Flip(fromIndex, toIndex);

                DoIterate(aa, bb, mode); // a problem here is from flip or doIterate

                fromIndex = Random().Next(sz + 80);
                toIndex = fromIndex + Random().Next((sz >> 1) + 1);
                aa = (BitArray)a.Clone();
                aa.Clear(fromIndex, toIndex);
                bb = (OpenBitSet)b.Clone();
                bb.Clear(fromIndex, toIndex);

                DoNextSetBit(aa, bb); // a problem here is from clear() or nextSetBit
                DoNextSetBitLong(aa, bb);

                DoPrevSetBit(aa, bb);
                DoPrevSetBitLong(aa, bb);

                fromIndex = Random().Next(sz + 80);
                toIndex = fromIndex + Random().Next((sz >> 1) + 1);
                aa = (BitArray)a.Clone();
                aa.Set(fromIndex, toIndex);
                bb = (OpenBitSet)b.Clone();
                bb.Set(fromIndex, toIndex);

                DoNextSetBit(aa, bb); // a problem here is from set() or nextSetBit
                DoNextSetBitLong(aa, bb);

                DoPrevSetBit(aa, bb);
                DoPrevSetBitLong(aa, bb);

                if (a0 != null)
                {
                    Assert.AreEqual(a.Equals(a0), b.Equals(b0));

                    Assert.AreEqual(a.Cardinality(), b.Cardinality());

                    BitArray a_and = (BitArray)a.Clone();
                    a_and = a_and.And(a0);
                    BitArray a_or = (BitArray)a.Clone();
                    a_or = a_or.Or(a0);
                    BitArray a_xor = (BitArray)a.Clone();
                    a_xor = a_xor.Xor(a0);
                    BitArray a_andn = (BitArray)a.Clone();
                    a_andn.AndNot(a0);

                    OpenBitSet b_and = (OpenBitSet)b.Clone();
                    Assert.AreEqual(b, b_and);
                    b_and.And(b0);
                    OpenBitSet b_or = (OpenBitSet)b.Clone();
                    b_or.Or(b0);
                    OpenBitSet b_xor = (OpenBitSet)b.Clone();
                    b_xor.Xor(b0);
                    OpenBitSet b_andn = (OpenBitSet)b.Clone();
                    b_andn.AndNot(b0);

                    DoIterate(a_and, b_and, mode);
                    DoIterate(a_or, b_or, mode);
                    DoIterate(a_xor, b_xor, mode);
                    DoIterate(a_andn, b_andn, mode);

                    Assert.AreEqual(a_and.Cardinality(), b_and.Cardinality());
                    Assert.AreEqual(a_or.Cardinality(), b_or.Cardinality());
                    Assert.AreEqual(a_xor.Cardinality(), b_xor.Cardinality());
                    Assert.AreEqual(a_andn.Cardinality(), b_andn.Cardinality());

                    // test non-mutating popcounts
                    Assert.AreEqual(b_and.Cardinality(), OpenBitSet.IntersectionCount(b, b0));
                    Assert.AreEqual(b_or.Cardinality(), OpenBitSet.UnionCount(b, b0));
                    Assert.AreEqual(b_xor.Cardinality(), OpenBitSet.XorCount(b, b0));
                    Assert.AreEqual(b_andn.Cardinality(), OpenBitSet.AndNotCount(b, b0));
                }

                a0 = a;
                b0 = b;
            }
        }

        // large enough to flush obvious bugs, small enough to run in <.5 sec as part of a
        // larger testsuite.
        [Test]
        public virtual void TestSmall()
        {
            DoRandomSets(AtLeast(1200), AtLeast(1000), 1);
            DoRandomSets(AtLeast(1200), AtLeast(1000), 2);
        }

        // uncomment to run a bigger test (~2 minutes).
        /*
        public void TestBig() {
          doRandomSets(2000,200000, 1);
          doRandomSets(2000,200000, 2);
        }
        */

        [Test]
        public virtual void TestEquals()
        {
            OpenBitSet b1 = new OpenBitSet(1111);
            OpenBitSet b2 = new OpenBitSet(2222);
            Assert.IsTrue(b1.Equals(b2));
            Assert.IsTrue(b2.Equals(b1));
            b1.Set(10);
            Assert.IsFalse(b1.Equals(b2));
            Assert.IsFalse(b2.Equals(b1));
            b2.Set(10);
            Assert.IsTrue(b1.Equals(b2));
            Assert.IsTrue(b2.Equals(b1));
            b2.Set(2221);
            Assert.IsFalse(b1.Equals(b2));
            Assert.IsFalse(b2.Equals(b1));
            b1.Set(2221);
            Assert.IsTrue(b1.Equals(b2));
            Assert.IsTrue(b2.Equals(b1));

            // try different type of object
            Assert.IsFalse(b1.Equals(new object()));
        }

        [Test]
        public virtual void TestHashCodeEquals()
        {
            OpenBitSet bs1 = new OpenBitSet(200);
            OpenBitSet bs2 = new OpenBitSet(64);
            bs1.Set(3);
            bs2.Set(3);
            Assert.AreEqual(bs1, bs2);
            Assert.AreEqual(bs1.GetHashCode(), bs2.GetHashCode());
        }

        private OpenBitSet MakeOpenBitSet(int[] a)
        {
            OpenBitSet bs = new OpenBitSet();
            foreach (int e in a)
            {
                bs.Set(e);
            }
            return bs;
        }

        private BitArray MakeBitSet(int[] a)
        {
            BitArray bs = new BitArray(a.Length);
            foreach (int e in a)
            {
                bs.Set(e, true);
            }
            return bs;
        }

        private void CheckPrevSetBitArray(int[] a)
        {
            OpenBitSet obs = MakeOpenBitSet(a);
            BitArray bs = MakeBitSet(a);
            DoPrevSetBit(bs, obs);
        }

        [Test]
        public virtual void TestPrevSetBit()
        {
            CheckPrevSetBitArray(new int[] { });
            CheckPrevSetBitArray(new int[] { 0 });
            CheckPrevSetBitArray(new int[] { 0, 2 });
        }

        [Test]
        public virtual void TestEnsureCapacity()
        {
            OpenBitSet bits = new OpenBitSet(1);
            int bit = Random().Next(100) + 10;
            bits.EnsureCapacity(bit); // make room for more bits
            bits.FastSet(bit - 1);
            Assert.IsTrue(bits.FastGet(bit - 1));
            bits.EnsureCapacity(bit + 1);
            bits.FastSet(bit);
            Assert.IsTrue(bits.FastGet(bit));
            bits.EnsureCapacity(3); // should not change numBits nor grow the array
            bits.FastSet(3);
            Assert.IsTrue(bits.FastGet(3));
            bits.FastSet(bit - 1);
            Assert.IsTrue(bits.FastGet(bit - 1));

            // test ensureCapacityWords
            int numWords = Random().Next(10) + 2; // make sure we grow the array (at least 128 bits)
            bits.EnsureCapacityWords(numWords);
            bit = TestUtil.NextInt(Random(), 127, (numWords << 6) - 1); // pick a bit >= to 128, but still within range
            bits.FastSet(bit);
            Assert.IsTrue(bits.FastGet(bit));
            bits.FastClear(bit);
            Assert.IsFalse(bits.FastGet(bit));
            bits.FastFlip(bit);
            Assert.IsTrue(bits.FastGet(bit));
            bits.EnsureCapacityWords(2); // should not change numBits nor grow the array
            bits.FastSet(3);
            Assert.IsTrue(bits.FastGet(3));
            bits.FastSet(bit - 1);
            Assert.IsTrue(bits.FastGet(bit - 1));
        }
    }
}