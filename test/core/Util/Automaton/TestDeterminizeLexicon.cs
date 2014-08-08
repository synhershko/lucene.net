using Lucene.Net.Index;
using Lucene.Net.Support;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace Lucene.Net.Util.Automaton
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

    /// <summary>
    /// Not thorough, but tries to test determinism correctness
    /// somewhat randomly, by determinizing a huge random lexicon.
    /// </summary>
    [TestFixture]
    public class TestDeterminizeLexicon : LuceneTestCase
    {
        [Test]
        public void TestLexicon()
        {
            var automata = new List<Automaton>();
            var terms = new List<string>();

            int num = AtLeast(1);
            for (int i = 0; i < num; i++)
            {
                automata.Clear();
                terms.Clear();
                for (int j = 0; j < 5000; j++)
                {
                    string randomString = TestUtil.RandomUnicodeString(Random());
                    terms.Add(randomString);
                    automata.Add(BasicAutomata.MakeString(randomString));
                }
                AssertLexicon(automata, terms);
            }
        }

        public void AssertLexicon(List<Automaton> a, List<string> terms)
        {
            var automata = CollectionsHelper.Shuffle(a);
            var lex = BasicOperations.Union(automata);
            lex.Determinize();
            Assert.IsTrue(SpecialOperations.IsFinite(lex));
            foreach (string s in terms)
            {
                Assert.IsTrue(BasicOperations.Run(lex, s));
            }
            var lexByte = new ByteRunAutomaton(lex);
            foreach (string s in terms)
            {
                sbyte[] bytes = s.GetBytes(Encoding.UTF8);
                Assert.IsTrue(lexByte.Run(bytes, 0, bytes.Length));
            }
        }
    }
}