using System;
using System.Globalization;

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

    /// <summary>
    /// Bayesian smoothing using Dirichlet priors. From Chengxiang Zhai and John
    /// Lafferty. 2001. A study of smoothing methods for language models applied to
    /// Ad Hoc information retrieval. In Proceedings of the 24th annual international
    /// ACM SIGIR conference on Research and development in information retrieval
    /// (SIGIR '01). ACM, New York, NY, USA, 334-342.
    /// <p>
    /// The formula as defined the paper assigns a negative score to documents that
    /// contain the term, but with fewer occurrences than predicted by the collection
    /// language model. The Lucene implementation returns {@code 0} for such
    /// documents.
    /// </p>
    ///
    /// @lucene.experimental
    /// </summary>
    public class LMDirichletSimilarity : LMSimilarity
    {
        /// <summary>
        /// The &mu; parameter. </summary>
        private readonly float Mu_Renamed;

        /// <summary>
        /// Instantiates the similarity with the provided &mu; parameter. </summary>
        public LMDirichletSimilarity(CollectionModel collectionModel, float mu)
            : base(collectionModel)
        {
            this.Mu_Renamed = mu;
        }

        /// <summary>
        /// Instantiates the similarity with the provided &mu; parameter. </summary>
        public LMDirichletSimilarity(float mu)
        {
            this.Mu_Renamed = mu;
        }

        /// <summary>
        /// Instantiates the similarity with the default &mu; value of 2000. </summary>
        public LMDirichletSimilarity(CollectionModel collectionModel)
            : this(collectionModel, 2000)
        {
        }

        /// <summary>
        /// Instantiates the similarity with the default &mu; value of 2000. </summary>
        public LMDirichletSimilarity()
            : this(2000)
        {
        }

        public override float Score(BasicStats stats, float freq, float docLen)
        {
            float score = stats.TotalBoost * (float)(Math.Log(1 + freq / (Mu_Renamed * ((LMStats)stats).CollectionProbability)) + Math.Log(Mu_Renamed / (docLen + Mu_Renamed)));
            return score > 0.0f ? score : 0.0f;
        }

        protected internal override void Explain(Explanation expl, BasicStats stats, int doc, float freq, float docLen)
        {
            if (stats.TotalBoost != 1.0f)
            {
                expl.AddDetail(new Explanation(stats.TotalBoost, "boost"));
            }

            expl.AddDetail(new Explanation(Mu_Renamed, "mu"));
            Explanation weightExpl = new Explanation();
            weightExpl.Value = (float)Math.Log(1 + freq / (Mu_Renamed * ((LMStats)stats).CollectionProbability));
            weightExpl.Description = "term weight";
            expl.AddDetail(weightExpl);
            expl.AddDetail(new Explanation((float)Math.Log(Mu_Renamed / (docLen + Mu_Renamed)), "document norm"));
            base.Explain(expl, stats, doc, freq, docLen);
        }

        /// <summary>
        /// Returns the &mu; parameter. </summary>
        public virtual float Mu
        {
            get
            {
                return Mu_Renamed;
            }
        }

        public override string Name
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "Dirichlet(%f)", Mu);
            }
        }
    }
}