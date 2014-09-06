using System.Collections.Generic;

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
    /// A <seealso cref="Scorer"/> which wraps another scorer and caches the score of the
    /// current document. Successive calls to <seealso cref="#score()"/> will return the same
    /// result and will not invoke the wrapped Scorer's score() method, unless the
    /// current document has changed.<br>
    /// this class might be useful due to the changes done to the <seealso cref="Collector"/>
    /// interface, in which the score is not computed for a document by default, only
    /// if the collector requests it. Some collectors may need to use the score in
    /// several places, however all they have in hand is a <seealso cref="Scorer"/> object, and
    /// might end up computing the score of a document more than once.
    /// </summary>
    public class ScoreCachingWrappingScorer : Scorer
    {
        private readonly Scorer Scorer;
        private int CurDoc = -1;
        private float CurScore;

        /// <summary>
        /// Creates a new instance by wrapping the given scorer. </summary>
        public ScoreCachingWrappingScorer(Scorer scorer)
            : base(scorer.weight)
        {
            this.Scorer = scorer;
        }

        public override float Score()
        {
            int doc = Scorer.DocID();
            if (doc != CurDoc)
            {
                CurScore = Scorer.Score();
                CurDoc = doc;
            }

            return CurScore;
        }

        public override int Freq()
        {
            return Scorer.Freq();
        }

        public override int DocID()
        {
            return Scorer.DocID();
        }

        public override int NextDoc()
        {
            return Scorer.NextDoc();
        }

        public override int Advance(int target)
        {
            return Scorer.Advance(target);
        }

        public override ICollection<ChildScorer> Children
        {
            get
            {
                //LUCENE TO-DO
                return new[] { new ChildScorer(Scorer, "CACHED") };
                //return Collections.singleton(new ChildScorer(Scorer, "CACHED"));
            }
        }

        public override long Cost()
        {
            return Scorer.Cost();
        }
    }
}