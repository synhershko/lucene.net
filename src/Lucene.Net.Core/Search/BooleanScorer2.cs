using System.Collections.Generic;
using System.Linq;

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

    using BooleanWeight = Lucene.Net.Search.BooleanQuery.BooleanWeight;

    /* See the description in BooleanScorer.java, comparing
     * BooleanScorer & BooleanScorer2 */

    /// <summary>
    /// An alternative to BooleanScorer that also allows a minimum number
    /// of optional scorers that should match.
    /// <br>Implements skipTo(), and has no limitations on the numbers of added scorers.
    /// <br>Uses ConjunctionScorer, DisjunctionScorer, ReqOptScorer and ReqExclScorer.
    /// </summary>
    internal class BooleanScorer2 : Scorer
    {
        private readonly IList<Scorer> RequiredScorers;
        private readonly IList<Scorer> OptionalScorers;
        private readonly IList<Scorer> ProhibitedScorers;

        private class Coordinator
        {
            private readonly BooleanScorer2 OuterInstance;

            internal readonly float[] CoordFactors;

            internal Coordinator(BooleanScorer2 outerInstance, int maxCoord, bool disableCoord)
            {
                this.OuterInstance = outerInstance;
                CoordFactors = new float[outerInstance.OptionalScorers.Count + outerInstance.RequiredScorers.Count + 1];
                for (int i = 0; i < CoordFactors.Length; i++)
                {
                    CoordFactors[i] = disableCoord ? 1.0f : ((BooleanWeight)outerInstance.weight).Coord(i, maxCoord);
                }
            }

            internal int NrMatchers; // to be increased by score() of match counting scorers.
        }

        private readonly Coordinator coordinator;

        /// <summary>
        /// The scorer to which all scoring will be delegated,
        /// except for computing and using the coordination factor.
        /// </summary>
        private readonly Scorer CountingSumScorer;

        /// <summary>
        /// The number of optionalScorers that need to match (if there are any) </summary>
        private readonly int MinNrShouldMatch;

        private int Doc = -1;

        /// <summary>
        /// Creates a <seealso cref="Scorer"/> with the given similarity and lists of required,
        /// prohibited and optional scorers. In no required scorers are added, at least
        /// one of the optional scorers will have to match during the search.
        /// </summary>
        /// <param name="weight">
        ///          The BooleanWeight to be used. </param>
        /// <param name="disableCoord">
        ///          If this parameter is true, coordination level matching
        ///          (<seealso cref="Similarity#coord(int, int)"/>) is not used. </param>
        /// <param name="minNrShouldMatch">
        ///          The minimum number of optional added scorers that should match
        ///          during the search. In case no required scorers are added, at least
        ///          one of the optional scorers will have to match during the search. </param>
        /// <param name="required">
        ///          the list of required scorers. </param>
        /// <param name="prohibited">
        ///          the list of prohibited scorers. </param>
        /// <param name="optional">
        ///          the list of optional scorers. </param>
        public BooleanScorer2(BooleanWeight weight, bool disableCoord, int minNrShouldMatch, IList<Scorer> required, IList<Scorer> prohibited, IList<Scorer> optional, int maxCoord)
            : base(weight)
        {
            if (minNrShouldMatch < 0)
            {
                throw new System.ArgumentException("Minimum number of optional scorers should not be negative");
            }
            this.MinNrShouldMatch = minNrShouldMatch;

            OptionalScorers = optional;
            RequiredScorers = required;
            ProhibitedScorers = prohibited;
            coordinator = new Coordinator(this, maxCoord, disableCoord);

            CountingSumScorer = MakeCountingSumScorer(disableCoord);
        }

        /// <summary>
        /// Count a scorer as a single match. </summary>
        private class SingleMatchScorer : Scorer
        {
            private readonly BooleanScorer2 OuterInstance;

            internal Scorer Scorer;
            internal int LastScoredDoc = -1;

            // Save the score of lastScoredDoc, so that we don't compute it more than
            // once in score().
            internal float LastDocScore = float.NaN;

            internal SingleMatchScorer(BooleanScorer2 outerInstance, Scorer scorer)
                : base(scorer.weight)
            {
                this.OuterInstance = outerInstance;
                this.Scorer = scorer;
            }

            public override float Score()
            {
                int doc = DocID();
                if (doc >= LastScoredDoc)
                {
                    if (doc > LastScoredDoc)
                    {
                        LastDocScore = Scorer.Score();
                        LastScoredDoc = doc;
                    }
                    OuterInstance.coordinator.NrMatchers++;
                }
                return LastDocScore;
            }

            public override int Freq()
            {
                return 1;
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

            public override long Cost()
            {
                return Scorer.Cost();
            }
        }

        private Scorer CountingDisjunctionSumScorer(IList<Scorer> scorers, int minNrShouldMatch)
        {
            // each scorer from the list counted as a single matcher
            if (minNrShouldMatch > 1)
            {
                return new MinShouldMatchSumScorerAnonymousInnerClassHelper(this, weight, scorers, minNrShouldMatch);
            }
            else
            {
                // we pass null for coord[] since we coordinate ourselves and override score()
                return new DisjunctionSumScorerAnonymousInnerClassHelper(this, weight, scorers.ToArray(), null);
            }
        }

        private class MinShouldMatchSumScorerAnonymousInnerClassHelper : MinShouldMatchSumScorer
        {
            private readonly BooleanScorer2 OuterInstance;

            public MinShouldMatchSumScorerAnonymousInnerClassHelper(BooleanScorer2 outerInstance, Lucene.Net.Search.Weight weight, IList<Scorer> scorers, int minNrShouldMatch)
                : base(weight, scorers, minNrShouldMatch)
            {
                this.OuterInstance = outerInstance;
            }

            public override float Score()
            {
                OuterInstance.coordinator.NrMatchers += base.NrMatchers;
                return base.Score();
            }
        }

        private class DisjunctionSumScorerAnonymousInnerClassHelper : DisjunctionSumScorer
        {
            private readonly BooleanScorer2 OuterInstance;

            public DisjunctionSumScorerAnonymousInnerClassHelper(BooleanScorer2 outerInstance, Weight weight, Scorer[] subScorers, float[] coord)
                : base(weight, subScorers, coord)
            {
                this.OuterInstance = outerInstance;
            }

            public override float Score()
            {
                OuterInstance.coordinator.NrMatchers += base.NrMatchers;
                return (float)base.score;
            }
        }

        private Scorer CountingConjunctionSumScorer(bool disableCoord, IList<Scorer> requiredScorers)
        {
            // each scorer from the list counted as a single matcher
            int requiredNrMatchers = requiredScorers.Count;
            return new ConjunctionScorerAnonymousInnerClassHelper(this, weight, requiredScorers.ToArray(), requiredNrMatchers);
        }

        private class ConjunctionScorerAnonymousInnerClassHelper : ConjunctionScorer
        {
            private readonly BooleanScorer2 OuterInstance;

            private int RequiredNrMatchers;

            public ConjunctionScorerAnonymousInnerClassHelper(BooleanScorer2 outerInstance, Weight weight, Scorer[] scorers, int requiredNrMatchers)
                : base(weight, scorers)
            {
                this.OuterInstance = outerInstance;
                this.RequiredNrMatchers = requiredNrMatchers;
                lastScoredDoc = -1;
                lastDocScore = float.NaN;
            }

            private int lastScoredDoc;

            // Save the score of lastScoredDoc, so that we don't compute it more than
            // once in score().
            private float lastDocScore;

            public override float Score()
            {
                int doc = OuterInstance.DocID();
                if (doc >= lastScoredDoc)
                {
                    if (doc > lastScoredDoc)
                    {
                        lastDocScore = base.Score();
                        lastScoredDoc = doc;
                    }
                    OuterInstance.coordinator.NrMatchers += RequiredNrMatchers;
                }
                // All scorers match, so defaultSimilarity super.score() always has 1 as
                // the coordination factor.
                // Therefore the sum of the scores of the requiredScorers
                // is used as score.
                return lastDocScore;
            }
        }

        private Scorer DualConjunctionSumScorer(bool disableCoord, Scorer req1, Scorer req2) // non counting.
        {
            return new ConjunctionScorer(weight, new Scorer[] { req1, req2 });
            // All scorers match, so defaultSimilarity always has 1 as
            // the coordination factor.
            // Therefore the sum of the scores of two scorers
            // is used as score.
        }

        /// <summary>
        /// Returns the scorer to be used for match counting and score summing.
        /// Uses requiredScorers, optionalScorers and prohibitedScorers.
        /// </summary>
        private Scorer MakeCountingSumScorer(bool disableCoord) // each scorer counted as a single matcher
        {
            return (RequiredScorers.Count == 0) ? MakeCountingSumScorerNoReq(disableCoord) : MakeCountingSumScorerSomeReq(disableCoord);
        }

        private Scorer MakeCountingSumScorerNoReq(bool disableCoord) // No required scorers
        {
            // minNrShouldMatch optional scorers are required, but at least 1
            int nrOptRequired = (MinNrShouldMatch < 1) ? 1 : MinNrShouldMatch;
            Scorer requiredCountingSumScorer;
            if (OptionalScorers.Count > nrOptRequired)
            {
                requiredCountingSumScorer = CountingDisjunctionSumScorer(OptionalScorers, nrOptRequired);
            }
            else if (OptionalScorers.Count == 1)
            {
                requiredCountingSumScorer = new SingleMatchScorer(this, OptionalScorers[0]);
            }
            else
            {
                requiredCountingSumScorer = CountingConjunctionSumScorer(disableCoord, OptionalScorers);
            }
            return AddProhibitedScorers(requiredCountingSumScorer);
        }

        private Scorer MakeCountingSumScorerSomeReq(bool disableCoord) // At least one required scorer.
        {
            if (OptionalScorers.Count == MinNrShouldMatch) // all optional scorers also required.
            {
                List<Scorer> allReq = new List<Scorer>(RequiredScorers);
                allReq.AddRange(OptionalScorers);
                return AddProhibitedScorers(CountingConjunctionSumScorer(disableCoord, allReq));
            } // optionalScorers.size() > minNrShouldMatch, and at least one required scorer
            else
            {
                Scorer requiredCountingSumScorer = RequiredScorers.Count == 1 ? new SingleMatchScorer(this, RequiredScorers[0]) : CountingConjunctionSumScorer(disableCoord, RequiredScorers);
                if (MinNrShouldMatch > 0) // use a required disjunction scorer over the optional scorers
                {
                    return AddProhibitedScorers(DualConjunctionSumScorer(disableCoord, requiredCountingSumScorer, CountingDisjunctionSumScorer(OptionalScorers, MinNrShouldMatch))); // non counting
                } // minNrShouldMatch == 0
                else
                {
                    return new ReqOptSumScorer(AddProhibitedScorers(requiredCountingSumScorer), OptionalScorers.Count == 1 ? new SingleMatchScorer(this, OptionalScorers[0])
                        // require 1 in combined, optional scorer.
                                    : CountingDisjunctionSumScorer(OptionalScorers, 1));
                }
            }
        }

        /// <summary>
        /// Returns the scorer to be used for match counting and score summing.
        /// Uses the given required scorer and the prohibitedScorers. </summary>
        /// <param name="requiredCountingSumScorer"> A required scorer already built. </param>
        private Scorer AddProhibitedScorers(Scorer requiredCountingSumScorer)
        {
            return (ProhibitedScorers.Count == 0) ? requiredCountingSumScorer : new ReqExclScorer(requiredCountingSumScorer, ((ProhibitedScorers.Count == 1) ? ProhibitedScorers[0] : new MinShouldMatchSumScorer(weight, ProhibitedScorers))); // no prohibited
        }

        public override int DocID()
        {
            return Doc;
        }

        public override int NextDoc()
        {
            return Doc = CountingSumScorer.NextDoc();
        }

        public override float Score()
        {
            coordinator.NrMatchers = 0;
            float sum = CountingSumScorer.Score();
            return sum * coordinator.CoordFactors[coordinator.NrMatchers];
        }

        public override int Freq()
        {
            return CountingSumScorer.Freq();
        }

        public override int Advance(int target)
        {
            return Doc = CountingSumScorer.Advance(target);
        }

        public override long Cost()
        {
            return CountingSumScorer.Cost();
        }

        public override ICollection<ChildScorer> Children
        {
            get
            {
                List<ChildScorer> children = new List<ChildScorer>();
                foreach (Scorer s in OptionalScorers)
                {
                    children.Add(new ChildScorer(s, "SHOULD"));
                }
                foreach (Scorer s in ProhibitedScorers)
                {
                    children.Add(new ChildScorer(s, "MUST_NOT"));
                }
                foreach (Scorer s in RequiredScorers)
                {
                    children.Add(new ChildScorer(s, "MUST"));
                }
                return children;
            }
        }
    }
}