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

using NUnit.Framework;

using IndexReader = Lucene.Net.Index.IndexReader;
using Term = Lucene.Net.Index.Term;

namespace Lucene.Net.Search
{
	
	/// <summary> A basic 'positive' Unit test class for the RangeFilter class.
	/// 
	/// <p>
	/// NOTE: at the moment, this class only tests for 'positive' results,
	/// it does not verify the results to ensure there are no 'false positives',
	/// nor does it adequately test 'negative' results.  It also does not test
	/// that garbage in results in an Exception.
	/// </summary>
    [TestFixture]
    public class TestRangeFilter : BaseTestRangeFilter
	{
		
        [Test]
        public virtual void  TestRangeFilterId()
		{
			
			IndexReader reader = IndexReader.Open(index);
			IndexSearcher search = new IndexSearcher(reader);
			
			int medId = ((maxId - minId) / 2);
			
			System.String minIP = Pad(minId);
			System.String maxIP = Pad(maxId);
			System.String medIP = Pad(medId);
			
			int numDocs = reader.NumDocs();
			
			Assert.AreEqual(numDocs, 1 + maxId - minId, "num of docs");
			
			Hits result;
			Query q = new TermQuery(new Term("body", "body"));
			
			// test id, bounded on both ends
			
			result = search.Search(q, new RangeFilter("id", minIP, maxIP, T, T));
			Assert.AreEqual(numDocs, result.Length(), "find all");
			
			result = search.Search(q, new RangeFilter("id", minIP, maxIP, T, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "all but last");
			
			result = search.Search(q, new RangeFilter("id", minIP, maxIP, F, T));
			Assert.AreEqual(numDocs - 1, result.Length(), "all but first");
			
			result = search.Search(q, new RangeFilter("id", minIP, maxIP, F, F));
			Assert.AreEqual(numDocs - 2, result.Length(), "all but ends");
			
			result = search.Search(q, new RangeFilter("id", medIP, maxIP, T, T));
			Assert.AreEqual(1 + maxId - medId, result.Length(), "med and up");
			
			result = search.Search(q, new RangeFilter("id", minIP, medIP, T, T));
			Assert.AreEqual(1 + medId - minId, result.Length(), "up to med");
			
			// unbounded id
			
			result = search.Search(q, new RangeFilter("id", minIP, null, T, F));
			Assert.AreEqual(numDocs, result.Length(), "min and up");
			
			result = search.Search(q, new RangeFilter("id", null, maxIP, F, T));
			Assert.AreEqual(numDocs, result.Length(), "max and down");
			
			result = search.Search(q, new RangeFilter("id", minIP, null, F, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "not min, but up");
			
			result = search.Search(q, new RangeFilter("id", null, maxIP, F, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "not max, but down");
			
			result = search.Search(q, new RangeFilter("id", medIP, maxIP, T, F));
			Assert.AreEqual(maxId - medId, result.Length(), "med and up, not max");
			
			result = search.Search(q, new RangeFilter("id", minIP, medIP, F, T));
			Assert.AreEqual(medId - minId, result.Length(), "not min, up to med");
			
			// very small sets
			
			result = search.Search(q, new RangeFilter("id", minIP, minIP, F, F));
			Assert.AreEqual(0, result.Length(), "min,min,F,F");
			result = search.Search(q, new RangeFilter("id", medIP, medIP, F, F));
			Assert.AreEqual(0, result.Length(), "med,med,F,F");
			result = search.Search(q, new RangeFilter("id", maxIP, maxIP, F, F));
			Assert.AreEqual(0, result.Length(), "max,max,F,F");
			
			result = search.Search(q, new RangeFilter("id", minIP, minIP, T, T));
			Assert.AreEqual(1, result.Length(), "min,min,T,T");
			result = search.Search(q, new RangeFilter("id", null, minIP, F, T));
			Assert.AreEqual(1, result.Length(), "nul,min,F,T");
			
			result = search.Search(q, new RangeFilter("id", maxIP, maxIP, T, T));
			Assert.AreEqual(1, result.Length(), "max,max,T,T");
			result = search.Search(q, new RangeFilter("id", maxIP, null, T, F));
			Assert.AreEqual(1, result.Length(), "max,nul,T,T");
			
			result = search.Search(q, new RangeFilter("id", medIP, medIP, T, T));
			Assert.AreEqual(1, result.Length(), "med,med,T,T");
		}
		
        [Test]
        public virtual void  TestRangeFilterRand()
		{
			
			IndexReader reader = IndexReader.Open(index);
			IndexSearcher search = new IndexSearcher(reader);
			
			System.String minRP = Pad(minR);
			System.String maxRP = Pad(maxR);
			
			int numDocs = reader.NumDocs();
			
			Assert.AreEqual(numDocs, 1 + maxId - minId, "num of docs");
			
			Hits result;
			Query q = new TermQuery(new Term("body", "body"));
			
			// test extremes, bounded on both ends
			
			result = search.Search(q, new RangeFilter("rand", minRP, maxRP, T, T));
			Assert.AreEqual(numDocs, result.Length(), "find all");
			
			result = search.Search(q, new RangeFilter("rand", minRP, maxRP, T, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "all but biggest");
			
			result = search.Search(q, new RangeFilter("rand", minRP, maxRP, F, T));
			Assert.AreEqual(numDocs - 1, result.Length(), "all but smallest");
			
			result = search.Search(q, new RangeFilter("rand", minRP, maxRP, F, F));
			Assert.AreEqual(numDocs - 2, result.Length(), "all but extremes");
			
			// unbounded
			
			result = search.Search(q, new RangeFilter("rand", minRP, null, T, F));
			Assert.AreEqual(numDocs, result.Length(), "smallest and up");
			
			result = search.Search(q, new RangeFilter("rand", null, maxRP, F, T));
			Assert.AreEqual(numDocs, result.Length(), "biggest and down");
			
			result = search.Search(q, new RangeFilter("rand", minRP, null, F, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "not smallest, but up");
			
			result = search.Search(q, new RangeFilter("rand", null, maxRP, F, F));
			Assert.AreEqual(numDocs - 1, result.Length(), "not biggest, but down");
			
			// very small sets
			
			result = search.Search(q, new RangeFilter("rand", minRP, minRP, F, F));
			Assert.AreEqual(0, result.Length(), "min,min,F,F");
			result = search.Search(q, new RangeFilter("rand", maxRP, maxRP, F, F));
			Assert.AreEqual(0, result.Length(), "max,max,F,F");
			
			result = search.Search(q, new RangeFilter("rand", minRP, minRP, T, T));
			Assert.AreEqual(1, result.Length(), "min,min,T,T");
			result = search.Search(q, new RangeFilter("rand", null, minRP, F, T));
			Assert.AreEqual(1, result.Length(), "nul,min,F,T");
			
			result = search.Search(q, new RangeFilter("rand", maxRP, maxRP, T, T));
			Assert.AreEqual(1, result.Length(), "max,max,T,T");
			result = search.Search(q, new RangeFilter("rand", maxRP, null, T, F));
			Assert.AreEqual(1, result.Length(), "max,nul,T,T");
		}
	}
}