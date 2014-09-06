namespace Lucene.Net.Search
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

    using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;

    /// <summary>
    /// Just counts the total number of hits.
    /// </summary>

    public class TotalHitCountCollector : Collector
    {
        private int TotalHits_Renamed;

        /// <summary>
        /// Returns how many hits matched the search. </summary>
        public virtual int TotalHits
        {
            get
            {
                return TotalHits_Renamed;
            }
        }

        public override Scorer Scorer
        {
            set
            {
            }
        }

        public override void Collect(int doc)
        {
            TotalHits_Renamed++;
        }

        public override AtomicReaderContext NextReader
        {
            set
            {
            }
        }

        public override bool AcceptsDocsOutOfOrder()
        {
            return true;
        }
    }
}