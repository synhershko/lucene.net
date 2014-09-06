/**
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

namespace Lucene.Net.Codecs.Bloom
{

    using Lucene.Net.Index;

    /// <summary>
    /// Class used to create index-time {@link FuzzySet} appropriately configured for
    /// each field. Also called to right-size bitsets for serialization.
    ///
    ///  @lucene.experimental
    /// </summary>
    public abstract class BloomFilterFactory
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state">The content to be indexed</param>
        /// <param name="info">The field requiring a BloomFilter</param>
        /// <returns>An appropriately sized set or null if no BloomFiltering required</returns>
        public abstract FuzzySet GetSetForField(SegmentWriteState state, FieldInfo info);

        /// <summary>
        /// Called when downsizing bitsets for serialization
        /// </summary>
        /// <param name="fieldInfo">The field with sparse set bits</param>
        /// <param name="initialSet">The bits accumulated</param>
        /// <returns> null or a hopefully more densely packed, smaller bitset</returns>
        public FuzzySet Downsize(FieldInfo fieldInfo, FuzzySet initialSet)
        {
            // Aim for a bitset size that would have 10% of bits set (so 90% of searches
            // would fail-fast)
            const float targetMaxSaturation = 0.1f;
            return initialSet.Downsize(targetMaxSaturation);
        }

        /// <summary>
        /// Used to determine if the given filter has reached saturation and should be retired i.e. not saved any more
        /// </summary>
        /// <param name="bloomFilter">The bloomFilter being tested</param>
        /// <param name="fieldInfo">The field with which this filter is associated</param>
        /// <returns>true if the set has reached saturation and should be retired</returns>
        public abstract bool IsSaturated(FuzzySet bloomFilter, FieldInfo fieldInfo);

    }
}
