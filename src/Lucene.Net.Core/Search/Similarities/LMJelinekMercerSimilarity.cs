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
    /// Language model based on the Jelinek-Mercer smoothing method. From Chengxiang
    /// Zhai and John Lafferty. 2001. A study of smoothing methods for language
    /// models applied to Ad Hoc information retrieval. In Proceedings of the 24th
    /// annual international ACM SIGIR conference on Research and development in
    /// information retrieval (SIGIR '01). ACM, New York, NY, USA, 334-342.
    /// <p>The model has a single parameter, &lambda;. According to said paper, the
    /// optimal value depends on both the collection and the query. The optimal value
    /// is around {@code 0.1} for title queries and {@code 0.7} for long queries.</p>
    ///
    /// @lucene.experimental
    /// </summary>
    public class LMJelinekMercerSimilarity : LMSimilarity
    {
        /// <summary>
        /// The &lambda; parameter. </summary>
        private readonly float Lambda_Renamed;

        /// <summary>
        /// Instantiates with the specified collectionModel and &lambda; parameter. </summary>
        public LMJelinekMercerSimilarity(CollectionModel collectionModel, float lambda)
            : base(collectionModel)
        {
            this.Lambda_Renamed = lambda;
        }

        /// <summary>
        /// Instantiates with the specified &lambda; parameter. </summary>
        public LMJelinekMercerSimilarity(float lambda)
        {
            this.Lambda_Renamed = lambda;
        }

        public override float Score(BasicStats stats, float freq, float docLen)
        {
            return stats.TotalBoost * (float)Math.Log(1 + ((1 - Lambda_Renamed) * freq / docLen) / (Lambda_Renamed * ((LMStats)stats).CollectionProbability));
        }

        protected internal override void Explain(Explanation expl, BasicStats stats, int doc, float freq, float docLen)
        {
            if (stats.TotalBoost != 1.0f)
            {
                expl.AddDetail(new Explanation(stats.TotalBoost, "boost"));
            }
            expl.AddDetail(new Explanation(Lambda_Renamed, "lambda"));
            base.Explain(expl, stats, doc, freq, docLen);
        }

        /// <summary>
        /// Returns the &lambda; parameter. </summary>
        public virtual float Lambda
        {
            get
            {
                return Lambda_Renamed;
            }
        }

        public override string Name
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "Jelinek-Mercer(%f)", Lambda);
            }
        }
    }
}