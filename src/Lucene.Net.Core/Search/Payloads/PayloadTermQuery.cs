namespace Lucene.Net.Search.Payloads
{
    using Lucene.Net.Index;

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
    using Bits = Lucene.Net.Util.Bits;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using DocsAndPositionsEnum = Lucene.Net.Index.DocsAndPositionsEnum;
    using Similarity = Lucene.Net.Search.Similarities.Similarity;
    using SpanQuery = Lucene.Net.Search.Spans.SpanQuery;
    using SpanScorer = Lucene.Net.Search.Spans.SpanScorer;
    using SpanTermQuery = Lucene.Net.Search.Spans.SpanTermQuery;
    using SpanWeight = Lucene.Net.Search.Spans.SpanWeight;
    using Term = Lucene.Net.Index.Term;
    using TermSpans = Lucene.Net.Search.Spans.TermSpans;

    /// <summary>
    /// this class is very similar to
    /// <seealso cref="Lucene.Net.Search.Spans.SpanTermQuery"/> except that it factors
    /// in the value of the payload located at each of the positions where the
    /// <seealso cref="Lucene.Net.Index.Term"/> occurs.
    /// <p/>
    /// NOTE: In order to take advantage of this with the default scoring implementation
    /// (<seealso cref="DefaultSimilarity"/>), you must override <seealso cref="DefaultSimilarity#scorePayload(int, int, int, BytesRef)"/>,
    /// which returns 1 by default.
    /// <p/>
    /// Payload scores are aggregated using a pluggable <seealso cref="PayloadFunction"/>. </summary>
    /// <seealso cref= Lucene.Net.Search.Similarities.Similarity.SimScorer#computePayloadFactor(int, int, int, BytesRef)
    ///  </seealso>
    public class PayloadTermQuery : SpanTermQuery
    {
        protected internal PayloadFunction Function;
        private bool IncludeSpanScore;

        public PayloadTermQuery(Term term, PayloadFunction function)
            : this(term, function, true)
        {
        }

        public PayloadTermQuery(Term term, PayloadFunction function, bool includeSpanScore)
            : base(term)
        {
            this.Function = function;
            this.IncludeSpanScore = includeSpanScore;
        }

        public override Weight CreateWeight(IndexSearcher searcher)
        {
            return new PayloadTermWeight(this, this, searcher);
        }

        protected internal class PayloadTermWeight : SpanWeight
        {
            private readonly PayloadTermQuery OuterInstance;

            public PayloadTermWeight(PayloadTermQuery outerInstance, PayloadTermQuery query, IndexSearcher searcher)
                : base(query, searcher)
            {
                this.OuterInstance = outerInstance;
            }

            public override Scorer Scorer(AtomicReaderContext context, Bits acceptDocs)
            {
                return new PayloadTermSpanScorer(this, (TermSpans)query.GetSpans(context, acceptDocs, TermContexts), this, Similarity.DoSimScorer(Stats, context));
            }

            protected internal class PayloadTermSpanScorer : SpanScorer
            {
                private readonly PayloadTermQuery.PayloadTermWeight OuterInstance;

                protected internal BytesRef Payload;
                protected internal float PayloadScore_Renamed;
                protected internal int PayloadsSeen;
                internal readonly TermSpans TermSpans;

                public PayloadTermSpanScorer(PayloadTermQuery.PayloadTermWeight outerInstance, TermSpans spans, Weight weight, Similarity.SimScorer docScorer)
                    : base(spans, weight, docScorer)
                {
                    this.OuterInstance = outerInstance;
                    TermSpans = spans;
                }

                protected internal override bool SetFreqCurrentDoc()
                {
                    if (!More)
                    {
                        return false;
                    }
                    Doc = Spans.Doc();
                    Freq_Renamed = 0.0f;
                    NumMatches = 0;
                    PayloadScore_Renamed = 0;
                    PayloadsSeen = 0;
                    while (More && Doc == Spans.Doc())
                    {
                        int matchLength = Spans.End() - Spans.Start();

                        Freq_Renamed += DocScorer.ComputeSlopFactor(matchLength);
                        NumMatches++;
                        ProcessPayload(OuterInstance.Similarity);

                        More = Spans.Next(); // this moves positions to the next match in this
                        // document
                    }
                    return More || (Freq_Renamed != 0);
                }

                protected internal virtual void ProcessPayload(Similarity similarity)
                {
                    if (TermSpans.PayloadAvailable)
                    {
                        DocsAndPositionsEnum postings = TermSpans.Postings;
                        Payload = postings.Payload;
                        if (Payload != null)
                        {
                            PayloadScore_Renamed = OuterInstance.OuterInstance.Function.CurrentScore(Doc, OuterInstance.OuterInstance.Term.Field(), Spans.Start(), Spans.End(), PayloadsSeen, PayloadScore_Renamed, DocScorer.ComputePayloadFactor(Doc, Spans.Start(), Spans.End(), Payload));
                        }
                        else
                        {
                            PayloadScore_Renamed = OuterInstance.OuterInstance.Function.CurrentScore(Doc, OuterInstance.OuterInstance.Term.Field(), Spans.Start(), Spans.End(), PayloadsSeen, PayloadScore_Renamed, 1F);
                        }
                        PayloadsSeen++;
                    }
                    else
                    {
                        // zero out the payload?
                    }
                }

                ///
                /// <returns> <seealso cref="#getSpanScore()"/> * <seealso cref="#getPayloadScore()"/> </returns>
                /// <exception cref="IOException"> if there is a low-level I/O error </exception>
                public override float Score()
                {
                    return OuterInstance.OuterInstance.IncludeSpanScore ? SpanScore * PayloadScore : PayloadScore;
                }

                /// <summary>
                /// Returns the SpanScorer score only.
                /// <p/>
                /// Should not be overridden without good cause!
                /// </summary>
                /// <returns> the score for just the Span part w/o the payload </returns>
                /// <exception cref="IOException"> if there is a low-level I/O error
                /// </exception>
                /// <seealso cref= #score() </seealso>
                protected internal virtual float SpanScore
                {
                    get
                    {
                        return base.Score();
                    }
                }

                /// <summary>
                /// The score for the payload
                /// </summary>
                /// <returns> The score, as calculated by
                ///         <seealso cref="PayloadFunction#docScore(int, String, int, float)"/> </returns>
                protected internal virtual float PayloadScore
                {
                    get
                    {
                        return OuterInstance.OuterInstance.Function.DocScore(Doc, OuterInstance.OuterInstance.Term.Field(), PayloadsSeen, PayloadScore_Renamed);
                    }
                }
            }

            public override Explanation Explain(AtomicReaderContext context, int doc)
            {
                PayloadTermSpanScorer scorer = (PayloadTermSpanScorer)Scorer(context, ((AtomicReader)context.Reader()).LiveDocs);
                if (scorer != null)
                {
                    int newDoc = scorer.Advance(doc);
                    if (newDoc == doc)
                    {
                        float freq = scorer.SloppyFreq();
                        Similarity.SimScorer docScorer = Similarity.DoSimScorer(Stats, context);
                        Explanation expl = new Explanation();
                        expl.Description = "weight(" + Query + " in " + doc + ") [" + Similarity.GetType().Name + "], result of:";
                        Explanation scoreExplanation = docScorer.Explain(doc, new Explanation(freq, "phraseFreq=" + freq));
                        expl.AddDetail(scoreExplanation);
                        expl.Value = scoreExplanation.Value;
                        // now the payloads part
                        // QUESTION: Is there a way to avoid this skipTo call? We need to know
                        // whether to load the payload or not
                        // GSI: I suppose we could toString the payload, but I don't think that
                        // would be a good idea
                        string field = ((SpanQuery)Query).Field;
                        Explanation payloadExpl = OuterInstance.Function.Explain(doc, field, scorer.PayloadsSeen, scorer.PayloadScore_Renamed);
                        payloadExpl.Value = scorer.PayloadScore;
                        // combined
                        ComplexExplanation result = new ComplexExplanation();
                        if (OuterInstance.IncludeSpanScore)
                        {
                            result.AddDetail(expl);
                            result.AddDetail(payloadExpl);
                            result.Value = expl.Value * payloadExpl.Value;
                            result.Description = "btq, product of:";
                        }
                        else
                        {
                            result.AddDetail(payloadExpl);
                            result.Value = payloadExpl.Value;
                            result.Description = "btq(includeSpanScore=false), result of:";
                        }
                        result.Match = true; // LUCENE-1303
                        return result;
                    }
                }

                return new ComplexExplanation(false, 0.0f, "no matching term");
            }
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = base.GetHashCode();
            result = prime * result + ((Function == null) ? 0 : Function.GetHashCode());
            result = prime * result + (IncludeSpanScore ? 1231 : 1237);
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (!base.Equals(obj))
            {
                return false;
            }
            if (this.GetType() != obj.GetType())
            {
                return false;
            }
            PayloadTermQuery other = (PayloadTermQuery)obj;
            if (Function == null)
            {
                if (other.Function != null)
                {
                    return false;
                }
            }
            else if (!Function.Equals(other.Function))
            {
                return false;
            }
            if (IncludeSpanScore != other.IncludeSpanScore)
            {
                return false;
            }
            return true;
        }
    }
}