using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

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

    [TestFixture]
    public class TestFilterDirectory : LuceneTestCase
    {
        [Test]
        public virtual void TestOverrides()
        {
            // verify that all methods of Directory are overridden by FilterDirectory,
            // except those under the 'exclude' list
            HashSet<MethodInfo> exclude = new HashSet<MethodInfo>();
            exclude.Add(typeof(Directory).GetMethod("Copy", new Type[] { typeof(Directory), typeof(string), typeof(string), typeof(IOContext) }));
            exclude.Add(typeof(Directory).GetMethod("CreateSlicer", new Type[] { typeof(string), typeof(IOContext) }));
            exclude.Add(typeof(Directory).GetMethod("OpenChecksumInput", new Type[] { typeof(string), typeof(IOContext) }));
            foreach (MethodInfo m in typeof(FilterDirectory).GetMethods())
            {
                if (m.DeclaringType == typeof(Directory))
                {
                    Assert.IsTrue(exclude.Contains(m), "method " + m.Name + " not overridden!");
                }
            }
        }
    }
}