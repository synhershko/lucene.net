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

    /// <summary>
    /// Holds one hit in <seealso cref="TopDocs"/>. </summary>

    public class ScoreDoc
    {
        /// <summary>
        /// The score of this document for the query. </summary>
        public float Score;

        /// <summary>
        /// A hit document's number. </summary>
        /// <seealso cref= IndexSearcher#doc(int)  </seealso>
        public int Doc;

        /// <summary>
        /// Only set by <seealso cref="TopDocs#merge"/> </summary>
        public int ShardIndex;

        /// <summary>
        /// Constructs a ScoreDoc. </summary>
        public ScoreDoc(int doc, float score)
            : this(doc, score, -1)
        {
        }

        /// <summary>
        /// Constructs a ScoreDoc. </summary>
        public ScoreDoc(int doc, float score, int shardIndex)
        {
            this.Doc = doc;
            this.Score = score;
            this.ShardIndex = shardIndex;
        }

        // A convenience method for debugging.
        public override string ToString()
        {
            return "doc=" + Doc + " score=" + Score + " shardIndex=" + ShardIndex;
        }
    }
}