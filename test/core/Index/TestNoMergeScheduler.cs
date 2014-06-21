using Lucene.Net.Support;

namespace Lucene.Net.Index
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

	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using Lucene.Net.Randomized.Generators;
    using NUnit.Framework;


	public class TestNoMergeScheduler : LuceneTestCase
	{

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testNoMergeScheduler() throws Exception
	  public virtual void TestNoMergeScheduler_Mem()
	  {
		MergeScheduler ms = NoMergeScheduler.INSTANCE;
		ms.Dispose();
		ms.Merge(null, RandomInts.RandomFrom(Random(), MergeTrigger.values()), Random().NextBoolean());
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testFinalSingleton() throws Exception
	  public virtual void TestFinalSingleton()
	  {
		Assert.IsTrue(Modifier.isFinal(typeof(NoMergeScheduler).Modifiers));
//JAVA TO C# CONVERTER TODO TASK: Java wildcard generics are not converted to .NET:
//ORIGINAL LINE: Constructor<?>[] ctors = NoMergeScheduler.class.getDeclaredConstructors();
		Constructor<?>[] ctors = typeof(NoMergeScheduler).DeclaredConstructors;
		Assert.AreEqual("expected 1 private ctor only: " + Arrays.ToString(ctors), 1, ctors.Length);
		Assert.IsTrue("that 1 should be private: " + ctors[0], Modifier.isPrivate(ctors[0].Modifiers));
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testMethodsOverridden() throws Exception
	  public virtual void TestMethodsOverridden()
	  {
		// Ensures that all methods of MergeScheduler are overridden. That's
		// important to ensure that NoMergeScheduler overrides everything, so that
		// no unexpected behavior/error occurs
		foreach (Method m in typeof(NoMergeScheduler).Methods)
		{
		  // getDeclaredMethods() returns just those methods that are declared on
		  // NoMergeScheduler. getMethods() returns those that are visible in that
		  // context, including ones from Object. So just filter out Object. If in
		  // the future MergeScheduler will extend a different class than Object,
		  // this will need to change.
		  if (m.DeclaringClass != typeof(object))
		  {
			Assert.IsTrue(m + " is not overridden !", m.DeclaringClass == typeof(NoMergeScheduler));
		  }
		}
	  }

	}

}