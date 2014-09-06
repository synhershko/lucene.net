using System;
using System.Linq;
using System.Reflection;

namespace Lucene.Net.Index
{
    using Lucene.Net.Randomized.Generators;
    using NUnit.Framework;

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

    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

    [TestFixture]
    public class TestNoMergeScheduler : LuceneTestCase
    {
        [Test]
        public virtual void TestNoMergeScheduler_Mem()
        {
            MergeScheduler ms = NoMergeScheduler.INSTANCE;
            ms.Dispose();
            ms.Merge(null, RandomInts.RandomFrom(Random(), Enum.GetValues(typeof(MergeTrigger)).Cast<MergeTrigger>().ToArray()), Random().NextBoolean());
        }

        //LUCENE TODO: Compilation problems
        /*[Test]
        public virtual void TestFinalSingleton()
	    {
		    Assert.IsTrue(Modifier.isFinal(typeof(NoMergeScheduler).Modifiers));
		    Constructor<?>[] ctors = typeof(NoMergeScheduler).DeclaredConstructors;
		    Assert.AreEqual("expected 1 private ctor only: " + Arrays.ToString(ctors), 1, ctors.Length);
		    Assert.IsTrue("that 1 should be private: " + ctors[0], Modifier.isPrivate(ctors[0].Modifiers));
	    }*/

        [Test]
        public virtual void TestMethodsOverridden()
        {
            // Ensures that all methods of MergeScheduler are overridden. That's
            // important to ensure that NoMergeScheduler overrides everything, so that
            // no unexpected behavior/error occurs
            foreach (MethodInfo m in typeof(NoMergeScheduler).GetMethods())
            {
                // getDeclaredMethods() returns just those methods that are declared on
                // NoMergeScheduler. getMethods() returns those that are visible in that
                // context, including ones from Object. So just filter out Object. If in
                // the future MergeScheduler will extend a different class than Object,
                // this will need to change.
                if (m.DeclaringType != typeof(object))
                {
                    Assert.IsTrue(m.DeclaringType == typeof(NoMergeScheduler), m + " is not overridden !");
                }
            }
        }
    }
}