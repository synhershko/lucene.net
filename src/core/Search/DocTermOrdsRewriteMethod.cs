using System.Diagnostics;
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


	using AtomicReaderContext = Lucene.Net.Index.AtomicReaderContext;
	using IndexReader = Lucene.Net.Index.IndexReader;
	using SortedSetDocValues = Lucene.Net.Index.SortedSetDocValues;
	using Terms = Lucene.Net.Index.Terms;
	using TermsEnum = Lucene.Net.Index.TermsEnum;
	using Bits = Lucene.Net.Util.Bits;
	using BytesRef = Lucene.Net.Util.BytesRef;
	using LongBitSet = Lucene.Net.Util.LongBitSet;

	/// <summary>
	/// Rewrites MultiTermQueries into a filter, using DocTermOrds for term enumeration.
	/// <p>
	/// this can be used to perform these queries against an unindexed docvalues field.
	/// @lucene.experimental
	/// </summary>
	public sealed class DocTermOrdsRewriteMethod : MultiTermQuery.RewriteMethod
	{

	  public override Query Rewrite(IndexReader reader, MultiTermQuery query)
	  {
		Query result = new ConstantScoreQuery(new MultiTermQueryDocTermOrdsWrapperFilter(query));
		result.Boost = query.Boost;
		return result;
	  }

	  internal class MultiTermQueryDocTermOrdsWrapperFilter : Filter
	  {

		protected internal readonly MultiTermQuery Query;

		/// <summary>
		/// Wrap a <seealso cref="MultiTermQuery"/> as a Filter.
		/// </summary>
		protected internal MultiTermQueryDocTermOrdsWrapperFilter(MultiTermQuery query)
		{
		  this.Query = query;
		}

		public override string ToString()
		{
		  // query.toString should be ok for the filter, too, if the query boost is 1.0f
		  return Query.ToString();
		}

		public override sealed bool Equals(object o)
		{
		  if (o == this)
		  {
			  return true;
		  }
		  if (o == null)
		  {
			  return false;
		  }
		  if (this.GetType().Equals(o.GetType()))
		  {
			return this.Query.Equals(((MultiTermQueryDocTermOrdsWrapperFilter)o).Query);
		  }
		  return false;
		}

		public override sealed int HashCode()
		{
		  return Query.HashCode();
		}

		/// <summary>
		/// Returns the field name for this query </summary>
		public string Field
		{
			get
			{
				return Query.Field;
			}
		}

		/// <summary>
		/// Returns a DocIdSet with documents that should be permitted in search
		/// results.
		/// </summary>
		public override DocIdSet GetDocIdSet(AtomicReaderContext context, Bits acceptDocs)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final Lucene.Net.Index.SortedSetDocValues docTermOrds = FieldCache_Fields.DEFAULT.getDocTermOrds(context.reader(), query.field);
		  SortedSetDocValues docTermOrds = FieldCache_Fields.DEFAULT.GetDocTermOrds(context.Reader(), Query.Field_Renamed);
		  // Cannot use FixedBitSet because we require long index (ord):
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final Lucene.Net.Util.LongBitSet termSet = new Lucene.Net.Util.LongBitSet(docTermOrds.getValueCount());
		  LongBitSet termSet = new LongBitSet(docTermOrds.ValueCount);
		  TermsEnum termsEnum = Query.GetTermsEnum(new TermsAnonymousInnerClassHelper(this, docTermOrds));

		  Debug.Assert(termsEnum != null);
		  if (termsEnum.Next() != null)
		  {
			// fill into a bitset
			do
			{
			  termSet.Set(termsEnum.Ord());
			} while (termsEnum.Next() != null);
		  }
		  else
		  {
			return null;
		  }

		  return new FieldCacheDocIdSetAnonymousInnerClassHelper(this, context.Reader().maxDoc(), acceptDocs, docTermOrds, termSet);
		}

		private class TermsAnonymousInnerClassHelper : Terms
		{
			private readonly MultiTermQueryDocTermOrdsWrapperFilter OuterInstance;

			private SortedSetDocValues DocTermOrds;

			public TermsAnonymousInnerClassHelper(MultiTermQueryDocTermOrdsWrapperFilter outerInstance, SortedSetDocValues docTermOrds)
			{
				this.OuterInstance = outerInstance;
				this.DocTermOrds = docTermOrds;
			}


			public override IComparer<BytesRef> Comparator
			{
				get
				{
				  return BytesRef.UTF8SortedAsUnicodeComparator;
				}
			}

			public override TermsEnum Iterator(TermsEnum reuse)
			{
			  return DocTermOrds.TermsEnum();
			}

			public override long SumTotalTermFreq
			{
				get
				{
				  return -1;
				}
			}

			public override long SumDocFreq
			{
				get
				{
				  return -1;
				}
			}

			public override int DocCount
			{
				get
				{
				  return -1;
				}
			}

			public override long Size()
			{
			  return -1;
			}

			public override bool HasFreqs()
			{
			  return false;
			}

			public override bool HasOffsets()
			{
			  return false;
			}

			public override bool HasPositions()
			{
			  return false;
			}

			public override bool HasPayloads()
			{
			  return false;
			}
		}

		private class FieldCacheDocIdSetAnonymousInnerClassHelper : FieldCacheDocIdSet
		{
			private readonly MultiTermQueryDocTermOrdsWrapperFilter OuterInstance;

			private SortedSetDocValues DocTermOrds;
			private LongBitSet TermSet;

			public FieldCacheDocIdSetAnonymousInnerClassHelper(MultiTermQueryDocTermOrdsWrapperFilter outerInstance, int maxDoc, Bits acceptDocs, SortedSetDocValues docTermOrds, LongBitSet termSet) : base(maxDoc, acceptDocs)
			{
				this.OuterInstance = outerInstance;
				this.DocTermOrds = docTermOrds;
				this.TermSet = termSet;
			}

			protected internal override sealed bool MatchDoc(int doc)
			{
			  DocTermOrds.Document = doc;
			  long ord;
			  // TODO: we could track max bit set and early terminate (since they come in sorted order)
			  while ((ord = DocTermOrds.NextOrd()) != SortedSetDocValues.NO_MORE_ORDS)
			  {
				if (TermSet.Get(ord))
				{
				  return true;
				}
			  }
			  return false;
			}
		}
	  }

	  public override bool Equals(object obj)
	  {
		if (this == obj)
		{
		  return true;
		}
		if (obj == null)
		{
		  return false;
		}
		if (this.GetType() != obj.GetType())
		{
		  return false;
		}
		return true;
	  }

	  public override int HashCode()
	  {
		return 877;
	  }
	}

}