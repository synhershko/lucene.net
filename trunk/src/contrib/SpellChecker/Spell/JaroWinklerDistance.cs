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

using System;
using System.Collections.Generic;
using System.Text;

namespace SpellChecker.Net.Search.Spell
{
    public class JaroWinklerDistance : StringDistance
    {
        private float threshold = 0.7f;

        private int[] Matches(String s1, String s2)
        {
            String Max, Min;
            if (s1.Length > s2.Length)
            {
                Max = s1;
                Min = s2;
            }
            else
            {
                Max = s2;
                Min = s1;
            }
            int range = Math.Max(Max.Length / 2 - 1, 0);
            int[] matchIndexes = new int[Min.Length];
            for (int i = 0; i < matchIndexes.Length; i++)
                matchIndexes[i] = -1;
            bool[] matchFlags = new bool[Max.Length];
            int matches = 0;
            for (int mi = 0; mi < Min.Length; mi++)
            {
                char c1 = Min[mi];
                for (int xi = Math.Max(mi - range, 0), xn = Math.Min(mi + range + 1, Max
                    .Length); xi < xn; xi++)
                {
                    if (!matchFlags[xi] && c1 == Max[xi])
                    {
                        matchIndexes[mi] = xi;
                        matchFlags[xi] = true;
                        matches++;
                        break;
                    }
                }
            }
            char[] ms1 = new char[matches];
            char[] ms2 = new char[matches];
            for (int i = 0, si = 0; i < Min.Length; i++)
            {
                if (matchIndexes[i] != -1)
                {
                    ms1[si] = Min[i];
                    si++;
                }
            }
            for (int i = 0, si = 0; i < Max.Length; i++)
            {
                if (matchFlags[i])
                {
                    ms2[si] = Max[i];
                    si++;
                }
            }
            int transpositions = 0;
            for (int mi = 0; mi < ms1.Length; mi++)
            {
                if (ms1[mi] != ms2[mi])
                {
                    transpositions++;
                }
            }
            int prefix = 0;
            for (int mi = 0; mi < Min.Length; mi++)
            {
                if (s1[mi] == s2[mi])
                {
                    prefix++;
                }
                else
                {
                    break;
                }
            }
            return new int[] { matches, transpositions / 2, prefix, Max.Length };
        }

        public float GetDistance(String s1, String s2)
        {
            int[] mtp = Matches(s1, s2);
            float m = (float)mtp[0];
            if (m == 0)
            {
                return 0f;
            }
            float j = ((m / s1.Length + m / s2.Length + (m - mtp[1]) / m)) / 3;
            float jw = j < GetThreshold() ? j : j + Math.Min(0.1f, 1f / mtp[3]) * mtp[2]
                * (1 - j);
            return jw;
        }

        /// <summary>
        ///Sets the threshold used to deterMine when Winkler bonus should be used.
        /// Set to a negative value to get the Jaro distance.
        /// </summary>
        /// <param name="threshold">the new value of the threshold</param>
        public void SetThreshold(float threshold)
        {
            this.threshold = threshold;
        }

        /// <summary>
        /// Returns the current value of the threshold used for adding the Winkler bonus.
        /// The default value is 0.7.
        /// </summary>
        /// <returns>the current value of the threshold</returns>
        public float GetThreshold()
        {
            return threshold;
        }

    }
}