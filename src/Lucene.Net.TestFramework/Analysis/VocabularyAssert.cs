using Lucene.Net.Util;
using NUnit.Framework;
using System.IO;

namespace Lucene.Net.Analysis
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
    /// Utility class for doing vocabulary-based stemming tests </summary>
    public class VocabularyAssert
    {
        /// <summary>
        /// Run a vocabulary test against two data files. </summary>
        public static void AssertVocabulary(Analyzer a, Stream voc, Stream @out)
        {
            TextReader vocReader = (TextReader)(new StreamReader(voc, IOUtils.CHARSET_UTF_8));
            TextReader outputReader = (TextReader)(new StreamReader(@out, IOUtils.CHARSET_UTF_8));
            string inputWord = null;
            while ((inputWord = vocReader.ReadLine()) != null)
            {
                string expectedWord = outputReader.ReadLine();
                Assert.IsNotNull(expectedWord);
                BaseTokenStreamTestCase.CheckOneTerm(a, inputWord, expectedWord);
            }
        }

        /// <summary>
        /// Run a vocabulary test against one file: tab separated. </summary>
        public static void AssertVocabulary(Analyzer a, Stream vocOut)
        {
            TextReader vocReader = (TextReader)(new StreamReader(vocOut, IOUtils.CHARSET_UTF_8));
            string inputLine = null;
            while ((inputLine = vocReader.ReadLine()) != null)
            {
                if (inputLine.StartsWith("#") || inputLine.Trim().Length == 0)
                {
                    continue; // comment
                }
                string[] words = inputLine.Split('\t');
                BaseTokenStreamTestCase.CheckOneTerm(a, words[0], words[1]);
            }
        }

        /* LUCENE TO-DO Removing until use proven
        /// <summary>
        /// Run a vocabulary test against two data files inside a zip file </summary>
        public static void AssertVocabulary(Analyzer a, File zipFile, string voc, string @out)
        {
          ZipFile zip = new ZipFile(zipFile);
          Stream v = zip.getInputStream(zip.getEntry(voc));
          Stream o = zip.getInputStream(zip.getEntry(@out));
          AssertVocabulary(a, v, o);
          v.Close();
          o.Close();
          zip.close();
        }

        /// <summary>
        /// Run a vocabulary test against a tab-separated data file inside a zip file </summary>
        public static void AssertVocabulary(Analyzer a, File zipFile, string vocOut)
        {
          ZipFile zip = new ZipFile(zipFile);
          Stream vo = zip.getInputStream(zip.getEntry(vocOut));
          AssertVocabulary(a, vo);
          vo.Close();
          zip.close();
        }*/
    }
}