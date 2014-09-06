using Lucene.Net.Support;
using NUnit.Framework;
using System;
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

    [TestFixture]
    public class TestCollectionUtil : LuceneTestCase
    {
        private CollectionsHelper.ReverseComparer<int> ReverseOrder = new CollectionsHelper.ReverseComparer<int>();

        private List<int> CreateRandomList(int maxSize)
        {
            Random rnd = Random();
            int[] a = new int[rnd.Next(maxSize) + 1];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = Convert.ToInt32(rnd.Next(a.Length));
            }
            return Arrays.AsList(a);
        }

        [Test]
        public virtual void TestIntroSort()
        {
            for (int i = 0, c = AtLeast(500); i < c; i++)
            {
                List<int> list1 = CreateRandomList(2000), list2 = new List<int>(list1);
                CollectionUtil.IntroSort(list1);
                list2.Sort();
                Assert.AreEqual(list2, list1);

                list1 = CreateRandomList(2000);
                list2 = new List<int>(list1);
                CollectionUtil.IntroSort<int>(list1, ReverseOrder);
                list2.Sort(ReverseOrder);
                Assert.AreEqual(list2, list1);
                // reverse back, so we can test that completely backwards sorted array (worst case) is working:
                CollectionUtil.IntroSort(list1);
                list2.Sort();
                Assert.AreEqual(list2, list1);
            }
        }

        [Test]
        public virtual void TestTimSort()
        {
            for (int i = 0, c = AtLeast(500); i < c; i++)
            {
                List<int> list1 = CreateRandomList(2000), list2 = new List<int>(list1);
                CollectionUtil.TimSort(list1);
                list2.Sort();
                Assert.AreEqual(list2, list1);

                list1 = CreateRandomList(2000);
                list2 = new List<int>(list1);
                CollectionUtil.TimSort(list1, ReverseOrder);
                list2.Sort(ReverseOrder);
                Assert.AreEqual(list2, list1);
                // reverse back, so we can test that completely backwards sorted array (worst case) is working:
                CollectionUtil.TimSort(list1);
                list2.Sort();
                Assert.AreEqual(list2, list1);
            }
        }

        [Test]
        public virtual void TestEmptyListSort()
        {
            // should produce no exceptions
            IList<int> list = Arrays.AsList(new int[0]); // LUCENE-2989
            CollectionUtil.IntroSort(list);
            CollectionUtil.TimSort(list);
            CollectionUtil.IntroSort(list, ReverseOrder);
            CollectionUtil.TimSort(list, ReverseOrder);

            // check that empty non-random access lists pass sorting without ex (as sorting is not needed)
            list = new List<int>();
            CollectionUtil.IntroSort(list);
            CollectionUtil.TimSort(list);
            CollectionUtil.IntroSort(list, ReverseOrder);
            CollectionUtil.TimSort(list, ReverseOrder);
        }

        [Test]
        public virtual void TestOneElementListSort()
        {
            // check that one-element non-random access lists pass sorting without ex (as sorting is not needed)
            IList<int> list = new List<int>();
            list.Add(1);
            CollectionUtil.IntroSort(list);
            CollectionUtil.TimSort(list);
            CollectionUtil.IntroSort(list, ReverseOrder);
            CollectionUtil.TimSort(list, ReverseOrder);
        }
    }
}