namespace Lucene.Net.Search.Similarities
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
    using BytesRef = Lucene.Net.Util.BytesRef;
    using FieldInvertState = Lucene.Net.Index.FieldInvertState;

    /// <summary>
    /// Implements the CombSUM method for combining evidence from multiple
    /// similarity values described in: Joseph A. Shaw, Edward A. Fox.
    /// In Text REtrieval Conference (1993), pp. 243-252
    /// @lucene.experimental
    /// </summary>
    public class MultiSimilarity : Similarity
    {
        /// <summary>
        /// the sub-similarities used to create the combined score </summary>
        protected internal readonly Similarity[] Sims;

        /// <summary>
        /// Creates a MultiSimilarity which will sum the scores
        /// of the provided <code>sims</code>.
        /// </summary>
        public MultiSimilarity(Similarity[] sims)
        {
            this.Sims = sims;
        }

        public override long ComputeNorm(FieldInvertState state)
        {
            return Sims[0].ComputeNorm(state);
        }

        public override SimWeight ComputeWeight(float queryBoost, CollectionStatistics collectionStats, params TermStatistics[] termStats)
        {
            SimWeight[] subStats = new SimWeight[Sims.Length];
            for (int i = 0; i < subStats.Length; i++)
            {
                subStats[i] = Sims[i].ComputeWeight(queryBoost, collectionStats, termStats);
            }
            return new MultiStats(subStats);
        }

        public override SimScorer DoSimScorer(SimWeight stats, AtomicReaderContext context)
        {
            SimScorer[] subScorers = new SimScorer[Sims.Length];
            for (int i = 0; i < subScorers.Length; i++)
            {
                subScorers[i] = Sims[i].DoSimScorer(((MultiStats)stats).SubStats[i], context);
            }
            return new MultiSimScorer(subScorers);
        }

        internal class MultiSimScorer : SimScorer
        {
            internal readonly SimScorer[] SubScorers;

            internal MultiSimScorer(SimScorer[] subScorers)
            {
                this.SubScorers = subScorers;
            }

            public override float Score(int doc, float freq)
            {
                float sum = 0.0f;
                foreach (SimScorer subScorer in SubScorers)
                {
                    sum += subScorer.Score(doc, freq);
                }
                return sum;
            }

            public override Explanation Explain(int doc, Explanation freq)
            {
                Explanation expl = new Explanation(Score(doc, freq.Value), "sum of:");
                foreach (SimScorer subScorer in SubScorers)
                {
                    expl.AddDetail(subScorer.Explain(doc, freq));
                }
                return expl;
            }

            public override float ComputeSlopFactor(int distance)
            {
                return SubScorers[0].ComputeSlopFactor(distance);
            }

            public override float ComputePayloadFactor(int doc, int start, int end, BytesRef payload)
            {
                return SubScorers[0].ComputePayloadFactor(doc, start, end, payload);
            }
        }

        internal class MultiStats : SimWeight
        {
            internal readonly SimWeight[] SubStats;

            internal MultiStats(SimWeight[] subStats)
            {
                this.SubStats = subStats;
            }

            public override float ValueForNormalization
            {
                get
                {
                    float sum = 0.0f;
                    foreach (SimWeight stat in SubStats)
                    {
                        sum += stat.ValueForNormalization;
                    }
                    return sum / SubStats.Length;
                }
            }

            public override void Normalize(float queryNorm, float topLevelBoost)
            {
                foreach (SimWeight stat in SubStats)
                {
                    stat.Normalize(queryNorm, topLevelBoost);
                }
            }
        }
    }
}