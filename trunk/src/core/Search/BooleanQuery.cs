/* 
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using Lucene.Net.Index;
using IndexReader = Lucene.Net.Index.IndexReader;
using ToStringUtils = Lucene.Net.Util.ToStringUtils;
using Occur = Lucene.Net.Search.BooleanClause.Occur;

namespace Lucene.Net.Search
{
	
	/// <summary>A Query that matches documents matching boolean combinations of other
	/// queries, e.g. <see cref="TermQuery" />s, <see cref="PhraseQuery" />s or other
	/// BooleanQuerys.
	/// </summary>
	[Serializable]
	public class BooleanQuery : Query, System.Collections.Generic.IEnumerable<BooleanClause>, System.ICloneable
	{
		[Serializable]
		private class AnonymousClassSimilarityDelegator:SimilarityDelegator
		{
			private void  InitBlock(BooleanQuery enclosingInstance)
			{
				this.enclosingInstance = enclosingInstance;
			}
			private BooleanQuery enclosingInstance;
			public BooleanQuery Enclosing_Instance
			{
				get
				{
					return enclosingInstance;
				}
				
			}
			internal AnonymousClassSimilarityDelegator(BooleanQuery enclosingInstance, Lucene.Net.Search.Similarity Param1):base(Param1)
			{
				InitBlock(enclosingInstance);
			}
			public override float Coord(int overlap, int maxOverlap)
			{
				return 1.0f;
			}
		}
		
		private static int maxClauseCount = 1024;
		
		/// <summary>Thrown when an attempt is made to add more than <see cref="GetMaxClauseCount()" />
		/// clauses. This typically happens if
		/// a PrefixQuery, FuzzyQuery, WildcardQuery, or TermRangeQuery 
		/// is expanded to many terms during search. 
		/// </summary>
		[Serializable]
		public class TooManyClauses:System.SystemException
		{
			public override System.String Message
			{
				get
				{
					return "maxClauseCount is set to " + Lucene.Net.Search.BooleanQuery.maxClauseCount;
				}
				
			}
		}
		
		/// <summary>Return the maximum number of clauses permitted, 1024 by default.
		/// Attempts to add more than the permitted number of clauses cause <see cref="TooManyClauses" />
		/// to be thrown.
		/// </summary>
		/// <seealso cref="SetMaxClauseCount(int)">
		/// </seealso>
		public static int GetMaxClauseCount()
		{
			return maxClauseCount;
		}
		
		/// <summary> Set the maximum number of clauses permitted per BooleanQuery.
		/// Default value is 1024.
		/// </summary>
		public static void  SetMaxClauseCount(int maxClauseCount)
		{
			if (maxClauseCount < 1)
				throw new System.ArgumentException("maxClauseCount must be >= 1");
			BooleanQuery.maxClauseCount = maxClauseCount;
		}
		
		private SupportClass.EquatableList<BooleanClause> clauses = new SupportClass.EquatableList<BooleanClause>();
		private bool disableCoord;
		
		/// <summary>Constructs an empty boolean query. </summary>
		public BooleanQuery()
		{
		}
		
		/// <summary>Constructs an empty boolean query.
		/// 
		/// <see cref="Similarity.Coord(int,int)" /> may be disabled in scoring, as
		/// appropriate. For example, this score factor does not make sense for most
		/// automatically generated queries, like <see cref="WildcardQuery" /> and <see cref="FuzzyQuery" />
		///.
		/// 
		/// </summary>
		/// <param name="disableCoord">disables <see cref="Similarity.Coord(int,int)" /> in scoring.
		/// </param>
		public BooleanQuery(bool disableCoord)
		{
			this.disableCoord = disableCoord;
		}
		
		/// <summary>Returns true iff <see cref="Similarity.Coord(int,int)" /> is disabled in
		/// scoring for this query instance.
		/// </summary>
		/// <seealso cref="BooleanQuery(bool)">
		/// </seealso>
		public virtual bool IsCoordDisabled()
		{
			return disableCoord;
		}
		
		// Implement coord disabling.
		// Inherit javadoc.
		public override Similarity GetSimilarity(Searcher searcher)
		{
			Similarity result = base.GetSimilarity(searcher);
			if (disableCoord)
			{
				// disable coord as requested
				result = new AnonymousClassSimilarityDelegator(this, result);
			}
			return result;
		}
		
		/// <summary> Specifies a minimum number of the optional BooleanClauses
		/// which must be satisfied.
		/// 
		/// <p/>
		/// By default no optional clauses are necessary for a match
		/// (unless there are no required clauses).  If this method is used,
		/// then the specified number of clauses is required.
		/// <p/>
		/// <p/>
		/// Use of this method is totally independent of specifying that
		/// any specific clauses are required (or prohibited).  This number will
		/// only be compared against the number of matching optional clauses.
		/// <p/>
		/// </summary>
		/// <param name="min">the number of optional clauses that must match</param>
		public virtual void  SetMinimumNumberShouldMatch(int min)
		{
			this.minNrShouldMatch = min;
		}
		protected internal int minNrShouldMatch = 0;
		
		/// <summary> Gets the minimum number of the optional BooleanClauses
		/// which must be satisifed.
		/// </summary>
		public virtual int GetMinimumNumberShouldMatch()
		{
			return minNrShouldMatch;
		}
		
		/// <summary>Adds a clause to a boolean query.
		/// 
		/// </summary>
		/// <throws>  TooManyClauses if the new number of clauses exceeds the maximum clause number </throws>
		/// <seealso cref="GetMaxClauseCount()">
		/// </seealso>
		public virtual void  Add(Query query, BooleanClause.Occur occur)
		{
			Add(new BooleanClause(query, occur));
		}
		
		/// <summary>Adds a clause to a boolean query.</summary>
		/// <throws>  TooManyClauses if the new number of clauses exceeds the maximum clause number </throws>
		/// <seealso cref="GetMaxClauseCount()">
		/// </seealso>
		public virtual void  Add(BooleanClause clause)
		{
			if (clauses.Count >= maxClauseCount)
				throw new TooManyClauses();
			
			clauses.Add(clause);
		}
		
		/// <summary>Returns the set of clauses in this query. </summary>
		public virtual BooleanClause[] GetClauses()
		{
			return clauses.ToArray();
		}
		
		/// <summary>Returns the list of clauses in this query. </summary>
		public virtual System.Collections.Generic.List<BooleanClause> Clauses()
		{
			return clauses;
		}
		
        /// <summary>
        /// Returns an iterator on the clauses in this query.
        /// </summary>
        /// <returns></returns>
        public System.Collections.Generic.IEnumerator<BooleanClause> GetEnumerator()
        {
            return clauses.GetEnumerator();
        }
		/// <summary> Expert: the Weight for BooleanQuery, used to
		/// normalize, score and explain these queries.
		/// 
		/// <p/>NOTE: this API and implementation is subject to
		/// change suddenly in the next release.<p/>
		/// </summary>
		[Serializable]
		protected internal class BooleanWeight:Weight
		{
			private void  InitBlock(BooleanQuery enclosingInstance)
			{
				this.enclosingInstance = enclosingInstance;
			}
			private BooleanQuery enclosingInstance;
			public BooleanQuery Enclosing_Instance
			{
				get
				{
					return enclosingInstance;
				}
				
			}
			/// <summary>The Similarity implementation. </summary>
			protected internal Similarity similarity;
			protected internal System.Collections.Generic.List<Weight> weights;
			
			public BooleanWeight(BooleanQuery enclosingInstance, Searcher searcher)
			{
				InitBlock(enclosingInstance);
				this.similarity = Enclosing_Instance.GetSimilarity(searcher);
                weights = new System.Collections.Generic.List<Weight>(Enclosing_Instance.clauses.Count);
				for (int i = 0; i < Enclosing_Instance.clauses.Count; i++)
				{
				    weights.Add(Enclosing_Instance.clauses[i].GetQuery().CreateWeight(searcher));
				}
			}
			
			public override Query GetQuery()
			{
				return Enclosing_Instance;
			}
			public override float GetValue()
			{
				return Enclosing_Instance.GetBoost();
			}
			
			public override float SumOfSquaredWeights()
			{
				float sum = 0.0f;
				for (int i = 0; i < weights.Count; i++)
				{
					// call sumOfSquaredWeights for all clauses in case of side effects
					float s = weights[i].SumOfSquaredWeights(); // sum sub weights
					if (!Enclosing_Instance.clauses[i].IsProhibited())
					// only add to sum for non-prohibited clauses
						sum += s;
				}
				
				sum *= Enclosing_Instance.GetBoost() * Enclosing_Instance.GetBoost(); // boost each sub-weight
				
				return sum;
			}
			
			
			public override void  Normalize(float norm)
			{
				norm *= Enclosing_Instance.GetBoost(); // incorporate boost
				foreach (Weight w in weights)
				{
					// normalize all clauses, (even if prohibited in case of side affects)
					w.Normalize(norm);
				}
			}
			
			public override Explanation Explain(IndexReader reader, int doc)
			{
				int minShouldMatch = Enclosing_Instance.GetMinimumNumberShouldMatch();
				ComplexExplanation sumExpl = new ComplexExplanation();
				sumExpl.SetDescription("sum of:");
				int coord = 0;
				int maxCoord = 0;
				float sum = 0.0f;
				bool fail = false;
				int shouldMatchCount = 0;
			    System.Collections.Generic.IEnumerator<BooleanClause> cIter = Enclosing_Instance.clauses.GetEnumerator();
				for (System.Collections.Generic.IEnumerator<Weight> wIter = weights.GetEnumerator(); wIter.MoveNext(); )
				{
                    cIter.MoveNext();
                    Weight w = wIter.Current;
					BooleanClause c = cIter.Current;
					if (w.Scorer(reader, true, true) == null)
					{
						continue;
					}
					Explanation e = w.Explain(reader, doc);
					if (!c.IsProhibited())
						maxCoord++;
					if (e.IsMatch())
					{
						if (!c.IsProhibited())
						{
							sumExpl.AddDetail(e);
							sum += e.GetValue();
							coord++;
						}
						else
						{
							Explanation r = new Explanation(0.0f, "match on prohibited clause (" + c.GetQuery().ToString() + ")");
							r.AddDetail(e);
							sumExpl.AddDetail(r);
							fail = true;
						}
						if (c.GetOccur() == Occur.SHOULD)
							shouldMatchCount++;
					}
					else if (c.IsRequired())
					{
						Explanation r = new Explanation(0.0f, "no match on required clause (" + c.GetQuery().ToString() + ")");
						r.AddDetail(e);
						sumExpl.AddDetail(r);
						fail = true;
					}
				}
				if (fail)
				{
					System.Boolean tempAux = false;
					sumExpl.SetMatch(tempAux);
					sumExpl.SetValue(0.0f);
					sumExpl.SetDescription("Failure to meet condition(s) of required/prohibited clause(s)");
					return sumExpl;
				}
				else if (shouldMatchCount < minShouldMatch)
				{
					System.Boolean tempAux2 = false;
					sumExpl.SetMatch(tempAux2);
					sumExpl.SetValue(0.0f);
					sumExpl.SetDescription("Failure to match minimum number " + "of optional clauses: " + minShouldMatch);
					return sumExpl;
				}
				
				sumExpl.SetMatch(0 < coord?true:false);
				sumExpl.SetValue(sum);
				
				float coordFactor = similarity.Coord(coord, maxCoord);
				if (coordFactor == 1.0f)
				// coord is no-op
					return sumExpl;
				// eliminate wrapper
				else
				{
					ComplexExplanation result = new ComplexExplanation(sumExpl.IsMatch(), sum * coordFactor, "product of:");
					result.AddDetail(sumExpl);
					result.AddDetail(new Explanation(coordFactor, "coord(" + coord + "/" + maxCoord + ")"));
					return result;
				}
			}
			
			public override Scorer Scorer(IndexReader reader, bool scoreDocsInOrder, bool topScorer)
			{
				var required = new System.Collections.Generic.List<Scorer>();
                var prohibited = new System.Collections.Generic.List<Scorer>();
                var optional = new System.Collections.Generic.List<Scorer>();

			    System.Collections.Generic.IEnumerator<BooleanClause> cIter = Enclosing_Instance.clauses.GetEnumerator();
				foreach (Weight w in weights)
				{
                    cIter.MoveNext();
					BooleanClause c = (BooleanClause) cIter.Current;
					Scorer subScorer = w.Scorer(reader, true, false);
					if (subScorer == null)
					{
						if (c.IsRequired())
						{
							return null;
						}
					}
					else if (c.IsRequired())
					{
						required.Add(subScorer);
					}
					else if (c.IsProhibited())
					{
						prohibited.Add(subScorer);
					}
					else
					{
						optional.Add(subScorer);
					}
				}
				
				// Check if we can return a BooleanScorer
				if (!scoreDocsInOrder && topScorer && required.Count == 0 && prohibited.Count < 32)
				{
					return new BooleanScorer(similarity, Enclosing_Instance.minNrShouldMatch, optional, prohibited);
				}
				
				if (required.Count == 0 && optional.Count == 0)
				{
					// no required and optional clauses.
					return null;
				}
				else if (optional.Count < Enclosing_Instance.minNrShouldMatch)
				{
					// either >1 req scorer, or there are 0 req scorers and at least 1
					// optional scorer. Therefore if there are not enough optional scorers
					// no documents will be matched by the query
					return null;
				}
				
				// Return a BooleanScorer2
				return new BooleanScorer2(similarity, Enclosing_Instance.minNrShouldMatch, required, prohibited, optional);
			}
			
			public override bool ScoresDocsOutOfOrder()
			{
				int numProhibited = 0;
				foreach (BooleanClause c in Enclosing_Instance.clauses)
				{
					if (c.IsRequired())
					{
						return false; // BS2 (in-order) will be used by scorer()
					}
					else if (c.IsProhibited())
					{
						++numProhibited;
					}
				}
				
				if (numProhibited > 32)
				{
					// cannot use BS
					return false;
				}
				
				// scorer() will return an out-of-order scorer if requested.
				return true;
			}
		}
		
		public override Weight CreateWeight(Searcher searcher)
		{
			return new BooleanWeight(this, searcher);
		}
		
		public override Query Rewrite(IndexReader reader)
		{
			if (minNrShouldMatch == 0 && clauses.Count == 1)
			{
				// optimize 1-clause queries
				BooleanClause c = clauses[0];
				if (!c.IsProhibited())
				{
					// just return clause
					
					Query query = c.GetQuery().Rewrite(reader); // rewrite first
					
					if (GetBoost() != 1.0f)
					{
						// incorporate boost
						if (query == c.GetQuery())
						// if rewrite was no-op
							query = (Query) query.Clone(); // then clone before boost
						query.SetBoost(GetBoost() * query.GetBoost());
					}
					
					return query;
				}
			}
			
			BooleanQuery clone = null; // recursively rewrite
			for (int i = 0; i < clauses.Count; i++)
			{
				BooleanClause c = clauses[i];
				Query query = c.GetQuery().Rewrite(reader);
				if (query != c.GetQuery())
				{
					// clause rewrote: must clone
					if (clone == null)
						clone = (BooleanQuery) this.Clone();
					clone.clauses[i] = new BooleanClause(query, c.GetOccur());
				}
			}
			if (clone != null)
			{
				return clone; // some clauses rewrote
			}
			else
				return this; // no clauses rewrote
		}
		
		// inherit javadoc
		public override void ExtractTerms(System.Collections.Generic.ISet<Term> terms)
		{
			foreach(BooleanClause clause in clauses)
			{
				clause.GetQuery().ExtractTerms(terms);
			}
		}
		
		public override System.Object Clone()
		{
			BooleanQuery clone = (BooleanQuery) base.Clone();
			clone.clauses = (SupportClass.EquatableList<BooleanClause>) this.clauses.Clone();
			return clone;
		}
		
		/// <summary>Prints a user-readable version of this query. </summary>
		public override System.String ToString(System.String field)
		{
			System.Text.StringBuilder buffer = new System.Text.StringBuilder();
			bool needParens = (GetBoost() != 1.0) || (GetMinimumNumberShouldMatch() > 0);
			if (needParens)
			{
				buffer.Append("(");
			}
			
			for (int i = 0; i < clauses.Count; i++)
			{
				BooleanClause c = clauses[i];
				if (c.IsProhibited())
					buffer.Append("-");
				else if (c.IsRequired())
					buffer.Append("+");
				
				Query subQuery = c.GetQuery();
				if (subQuery != null)
				{
					if (subQuery is BooleanQuery)
					{
						// wrap sub-bools in parens
						buffer.Append("(");
						buffer.Append(subQuery.ToString(field));
						buffer.Append(")");
					}
					else
					{
						buffer.Append(subQuery.ToString(field));
					}
				}
				else
				{
					buffer.Append("null");
				}
				
				if (i != clauses.Count - 1)
					buffer.Append(" ");
			}
			
			if (needParens)
			{
				buffer.Append(")");
			}
			
			if (GetMinimumNumberShouldMatch() > 0)
			{
				buffer.Append('~');
				buffer.Append(GetMinimumNumberShouldMatch());
			}
			
			if (GetBoost() != 1.0f)
			{
				buffer.Append(ToStringUtils.Boost(GetBoost()));
			}
			
			return buffer.ToString();
		}
		
		/// <summary>Returns true iff <c>o</c> is equal to this. </summary>
		public  override bool Equals(System.Object o)
		{
            if (!(o is BooleanQuery))
                return false;
            BooleanQuery other = (BooleanQuery)o;
            return (this.GetBoost() == other.GetBoost())
                    && this.clauses.Equals(other.clauses)
                    && this.GetMinimumNumberShouldMatch() == other.GetMinimumNumberShouldMatch()
                    && this.disableCoord == other.disableCoord;
		}
		
		/// <summary>Returns a hash code value for this object.</summary>
		public override int GetHashCode()
		{
            return BitConverter.ToInt32(BitConverter.GetBytes(GetBoost()), 0) ^ clauses.GetHashCode() + GetMinimumNumberShouldMatch() + (disableCoord ? 17 : 0);
		}

	    IEnumerator IEnumerable.GetEnumerator()
	    {
	        return GetEnumerator();
	    }
	}
}