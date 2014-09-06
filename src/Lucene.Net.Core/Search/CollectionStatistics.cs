using System.Diagnostics;

namespace Lucene.Net.Search
{
    // javadocs
    // javadocs

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
    /// Contains statistics for a collection (field)
    /// @lucene.experimental
    /// </summary>
    public class CollectionStatistics
    {
        private readonly string Field_Renamed;
        private readonly long MaxDoc_Renamed;
        private readonly long DocCount_Renamed;
        private readonly long SumTotalTermFreq_Renamed;
        private readonly long SumDocFreq_Renamed;

        public CollectionStatistics(string field, long maxDoc, long docCount, long sumTotalTermFreq, long sumDocFreq)
        {
            Debug.Assert(maxDoc >= 0);
            Debug.Assert(docCount >= -1 && docCount <= maxDoc); // #docs with field must be <= #docs
            Debug.Assert(sumDocFreq == -1 || sumDocFreq >= docCount); // #postings must be >= #docs with field
            Debug.Assert(sumTotalTermFreq == -1 || sumTotalTermFreq >= sumDocFreq); // #positions must be >= #postings
            this.Field_Renamed = field;
            this.MaxDoc_Renamed = maxDoc;
            this.DocCount_Renamed = docCount;
            this.SumTotalTermFreq_Renamed = sumTotalTermFreq;
            this.SumDocFreq_Renamed = sumDocFreq;
        }

        /// <summary>
        /// returns the field name </summary>
        public string Field()
        {
            return Field_Renamed;
        }

        /// <summary>
        /// returns the total number of documents, regardless of
        /// whether they all contain values for this field. </summary>
        /// <seealso cref= IndexReader#maxDoc()  </seealso>
        public long MaxDoc()
        {
            return MaxDoc_Renamed;
        }

        /// <summary>
        /// returns the total number of documents that
        /// have at least one term for this field. </summary>
        /// <seealso cref= Terms#getDocCount()  </seealso>
        public long DocCount()
        {
            return DocCount_Renamed;
        }

        /// <summary>
        /// returns the total number of tokens for this field </summary>
        /// <seealso cref= Terms#getSumTotalTermFreq()  </seealso>
        public long SumTotalTermFreq()
        {
            return SumTotalTermFreq_Renamed;
        }

        /// <summary>
        /// returns the total number of postings for this field </summary>
        /// <seealso cref= Terms#getSumDocFreq()  </seealso>
        public long SumDocFreq()
        {
            return SumDocFreq_Renamed;
        }
    }
}