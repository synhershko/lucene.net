using System.Collections.Generic;

namespace Lucene.Net.Store
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
	using Test = org.junit.Test;

	public class TestFilterDirectory : LuceneTestCase
	{

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testOverrides() throws Exception
	  public virtual void TestOverrides()
	  {
		// verify that all methods of Directory are overridden by FilterDirectory,
		// except those under the 'exclude' list
		Set<Method> exclude = new HashSet<Method>();
		exclude.add(typeof(Directory).getMethod("copy", typeof(Directory), typeof(string), typeof(string), typeof(IOContext)));
		exclude.add(typeof(Directory).getMethod("createSlicer", typeof(string), typeof(IOContext)));
		exclude.add(typeof(Directory).getMethod("openChecksumInput", typeof(string), typeof(IOContext)));
		foreach (Method m in typeof(FilterDirectory).Methods)
		{
		  if (m.DeclaringClass == typeof(Directory))
		  {
			Assert.IsTrue("method " + m.Name + " not overridden!", exclude.contains(m));
		  }
		}
	  }

	}

}