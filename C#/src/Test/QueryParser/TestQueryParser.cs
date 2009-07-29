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

using Analyzer = Lucene.Net.Analysis.Analyzer;
using KeywordAnalyzer = Lucene.Net.Analysis.KeywordAnalyzer;
using LowerCaseTokenizer = Lucene.Net.Analysis.LowerCaseTokenizer;
using SimpleAnalyzer = Lucene.Net.Analysis.SimpleAnalyzer;
using StopAnalyzer = Lucene.Net.Analysis.StopAnalyzer;
using StopFilter = Lucene.Net.Analysis.StopFilter;
using TokenFilter = Lucene.Net.Analysis.TokenFilter;
using TokenStream = Lucene.Net.Analysis.TokenStream;
using WhitespaceAnalyzer = Lucene.Net.Analysis.WhitespaceAnalyzer;
using StandardAnalyzer = Lucene.Net.Analysis.Standard.StandardAnalyzer;
using DateField = Lucene.Net.Documents.DateField;
using DateTools = Lucene.Net.Documents.DateTools;
using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using Term = Lucene.Net.Index.Term;
using BooleanQuery = Lucene.Net.Search.BooleanQuery;
using ConstantScoreRangeQuery = Lucene.Net.Search.ConstantScoreRangeQuery;
using FuzzyQuery = Lucene.Net.Search.FuzzyQuery;
using IndexSearcher = Lucene.Net.Search.IndexSearcher;
using MatchAllDocsQuery = Lucene.Net.Search.MatchAllDocsQuery;
using PhraseQuery = Lucene.Net.Search.PhraseQuery;
using PrefixQuery = Lucene.Net.Search.PrefixQuery;
using Query = Lucene.Net.Search.Query;
using RangeQuery = Lucene.Net.Search.RangeQuery;
using ScoreDoc = Lucene.Net.Search.ScoreDoc;
using TermQuery = Lucene.Net.Search.TermQuery;
using WildcardQuery = Lucene.Net.Search.WildcardQuery;
using RAMDirectory = Lucene.Net.Store.RAMDirectory;
using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

namespace Lucene.Net.QueryParsers
{
	
	/// <summary> Tests QueryParser.</summary>
	[TestFixture]
	public class TestQueryParser : LuceneTestCase
	{
		public class AnonymousClassQueryParser : Lucene.Net.QueryParsers.QueryParser
		{
			private void  InitBlock(int[] type, TestQueryParser enclosingInstance)
			{
				this.type = type;
				this.enclosingInstance = enclosingInstance;
			}
			private int[] type;
			private TestQueryParser enclosingInstance;
			public TestQueryParser Enclosing_Instance
			{
				get
				{
					return enclosingInstance;
				}
				
			}
			internal AnonymousClassQueryParser(int[] type, TestQueryParser enclosingInstance, System.String Param1, Lucene.Net.Analysis.Analyzer Param2):base(Param1, Param2)
			{
				InitBlock(type, enclosingInstance);
			}
			public override Lucene.Net.Search.Query GetWildcardQuery(System.String field, System.String termStr)
			{
				// override error checking of superclass
				type[0] = 1;
				return new TermQuery(new Term(field, termStr));
			}
			public override Lucene.Net.Search.Query GetPrefixQuery(System.String field, System.String termStr)
			{
				// override error checking of superclass
				type[0] = 2;
				return new TermQuery(new Term(field, termStr));
			}
			
			public override Lucene.Net.Search.Query GetFieldQuery(System.String field, System.String queryText)
			{
				type[0] = 3;
				return base.GetFieldQuery(field, queryText);
			}
		}
		
		public static Analyzer qpAnalyzer = new QPTestAnalyzer();
		
		public class QPTestFilter : TokenFilter
		{
			/// <summary> Filter which discards the token 'stop' and which expands the
			/// token 'phrase' into 'phrase1 phrase2'
			/// </summary>
			public QPTestFilter(TokenStream in_Renamed) : base(in_Renamed)
			{
			}
			
			internal bool inPhrase = false;
			internal int savedStart = 0, savedEnd = 0;
			
			public override Lucene.Net.Analysis.Token Next(Lucene.Net.Analysis.Token reusableToken)
			{
                System.Diagnostics.Debug.Assert(reusableToken != null);
				if (inPhrase)
				{
					inPhrase = false;
					return reusableToken.Reinit("phrase2", savedStart, savedEnd);
				}
				else
					for (Lucene.Net.Analysis.Token nextToken = input.Next(reusableToken); nextToken != null; nextToken = input.Next(reusableToken))
					{
						if (nextToken.Term().Equals("phrase"))
						{
							inPhrase = true;
							savedStart = nextToken.StartOffset();
							savedEnd = nextToken.EndOffset();
							return nextToken.Reinit("phrase1", savedStart, savedEnd);
						}
						else if (!nextToken.Term().Equals("stop"))
							return nextToken;
					}
				return null;
			}
		}
		
		public class QPTestAnalyzer : Analyzer
		{
			
			/// <summary>Filters LowerCaseTokenizer with StopFilter. </summary>
			public override TokenStream TokenStream(System.String fieldName, System.IO.TextReader reader)
			{
				return new QPTestFilter(new LowerCaseTokenizer(reader));
			}
		}
		
		public class QPTestParser : Lucene.Net.QueryParsers.QueryParser
		{
			public QPTestParser(System.String f, Analyzer a):base(f, a)
			{
			}
			
			public override Query GetFuzzyQuery(System.String field, System.String termStr, float minSimilarity)
			{
				throw new ParseException("Fuzzy queries not allowed");
			}
			
			public override Query GetWildcardQuery(System.String field, System.String termStr)
			{
				throw new ParseException("Wildcard queries not allowed");
			}
		}
		
		private int originalMaxClauses;
		
		[SetUp]
		public override void SetUp()
		{
			base.SetUp();
			originalMaxClauses = BooleanQuery.GetMaxClauseCount();
		}
		
		public virtual Lucene.Net.QueryParsers.QueryParser GetParser(Analyzer a)
		{
			if (a == null)
				a = new SimpleAnalyzer();
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", a);
			qp.SetDefaultOperator(Lucene.Net.QueryParsers.QueryParser.OR_OPERATOR);
			return qp;
		}
		
		public virtual Query GetQuery(System.String query, Analyzer a)
		{
			return GetParser(a).Parse(query);
		}
		
		public virtual void  AssertQueryEquals(System.String query, Analyzer a, System.String result)
		{
			Query q = GetQuery(query, a);
			System.String s = q.ToString("field");
			if (!s.Equals(result))
			{
				Assert.Fail("Query /" + query + "/ yielded /" + s + "/, expecting /" + result + "/");
			}
		}
		
		public virtual void  AssertQueryEquals(Lucene.Net.QueryParsers.QueryParser qp, System.String field, System.String query, System.String result)
		{
			Query q = qp.Parse(query);
			System.String s = q.ToString(field);
			if (!s.Equals(result))
			{
				Assert.Fail("Query /" + query + "/ yielded /" + s + "/, expecting /" + result + "/");
			}
		}
		
		public virtual void  AssertEscapedQueryEquals(System.String query, Analyzer a, System.String result)
		{
			System.String escapedQuery = Lucene.Net.QueryParsers.QueryParser.Escape(query);
			if (!escapedQuery.Equals(result))
			{
				Assert.Fail("Query /" + query + "/ yielded /" + escapedQuery + "/, expecting /" + result + "/");
			}
		}
		
		public virtual void  AssertWildcardQueryEquals(System.String query, bool lowercase, System.String result, bool allowLeadingWildcard)
		{
			Lucene.Net.QueryParsers.QueryParser qp = GetParser(null);
			qp.SetLowercaseExpandedTerms(lowercase);
			qp.SetAllowLeadingWildcard(allowLeadingWildcard);
			Query q = qp.Parse(query);
			System.String s = q.ToString("field");
			if (!s.Equals(result))
			{
				Assert.Fail("WildcardQuery /" + query + "/ yielded /" + s + "/, expecting /" + result + "/");
			}
		}
		
		public virtual void  AssertWildcardQueryEquals(System.String query, bool lowercase, System.String result)
		{
			AssertWildcardQueryEquals(query, lowercase, result, false);
		}
		
		public virtual void  AssertWildcardQueryEquals(System.String query, System.String result)
		{
			Lucene.Net.QueryParsers.QueryParser qp = GetParser(null);
			Query q = qp.Parse(query);
			System.String s = q.ToString("field");
			if (!s.Equals(result))
			{
				Assert.Fail("WildcardQuery /" + query + "/ yielded /" + s + "/, expecting /" + result + "/");
			}
		}
		
		public virtual Query GetQueryDOA(System.String query, Analyzer a)
		{
			if (a == null)
				a = new SimpleAnalyzer();
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", a);
			qp.SetDefaultOperator(Lucene.Net.QueryParsers.QueryParser.AND_OPERATOR);
			return qp.Parse(query);
		}
		
		public virtual void  AssertQueryEqualsDOA(System.String query, Analyzer a, System.String result)
		{
			Query q = GetQueryDOA(query, a);
			System.String s = q.ToString("field");
			if (!s.Equals(result))
			{
				Assert.Fail("Query /" + query + "/ yielded /" + s + "/, expecting /" + result + "/");
			}
		}
		
		[Test]
		public virtual void  TestSimple()
		{
			AssertQueryEquals("term term term", null, "term term term");
			AssertQueryEquals("türm term term", new WhitespaceAnalyzer(), "türm term term");
			AssertQueryEquals("ümlaut", new WhitespaceAnalyzer(), "ümlaut");
			
			AssertQueryEquals("\"\"", new KeywordAnalyzer(), "");
			AssertQueryEquals("foo:\"\"", new KeywordAnalyzer(), "foo:");
			
			AssertQueryEquals("a AND b", null, "+a +b");
			AssertQueryEquals("(a AND b)", null, "+a +b");
			AssertQueryEquals("c OR (a AND b)", null, "c (+a +b)");
			AssertQueryEquals("a AND NOT b", null, "+a -b");
			AssertQueryEquals("a AND -b", null, "+a -b");
			AssertQueryEquals("a AND !b", null, "+a -b");
			AssertQueryEquals("a && b", null, "+a +b");
			AssertQueryEquals("a && ! b", null, "+a -b");
			
			AssertQueryEquals("a OR b", null, "a b");
			AssertQueryEquals("a || b", null, "a b");
			AssertQueryEquals("a OR !b", null, "a -b");
			AssertQueryEquals("a OR ! b", null, "a -b");
			AssertQueryEquals("a OR -b", null, "a -b");
			
			AssertQueryEquals("+term -term term", null, "+term -term term");
			AssertQueryEquals("foo:term AND field:anotherTerm", null, "+foo:term +anotherterm");
			AssertQueryEquals("term AND \"phrase phrase\"", null, "+term +\"phrase phrase\"");
			AssertQueryEquals("\"hello there\"", null, "\"hello there\"");
			Assert.IsTrue(GetQuery("a AND b", null) is BooleanQuery);
			Assert.IsTrue(GetQuery("hello", null) is TermQuery);
			Assert.IsTrue(GetQuery("\"hello there\"", null) is PhraseQuery);
			
			AssertQueryEquals("germ term^2.0", null, "germ term^2.0");
			AssertQueryEquals("(term)^2.0", null, "term^2.0");
			AssertQueryEquals("(germ term)^2.0", null, "(germ term)^2.0");
			AssertQueryEquals("term^2.0", null, "term^2.0");
			AssertQueryEquals("term^2", null, "term^2.0");
			AssertQueryEquals("\"germ term\"^2.0", null, "\"germ term\"^2.0");
			AssertQueryEquals("\"term germ\"^2", null, "\"term germ\"^2.0");
			
			AssertQueryEquals("(foo OR bar) AND (baz OR boo)", null, "+(foo bar) +(baz boo)");
			AssertQueryEquals("((a OR b) AND NOT c) OR d", null, "(+(a b) -c) d");
			AssertQueryEquals("+(apple \"steve jobs\") -(foo bar baz)", null, "+(apple \"steve jobs\") -(foo bar baz)");
			AssertQueryEquals("+title:(dog OR cat) -author:\"bob dole\"", null, "+(title:dog title:cat) -author:\"bob dole\"");
			
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new StandardAnalyzer());
			// make sure OR is the default:
			Assert.AreEqual(Lucene.Net.QueryParsers.QueryParser.OR_OPERATOR, qp.GetDefaultOperator());
			qp.SetDefaultOperator(Lucene.Net.QueryParsers.QueryParser.AND_OPERATOR);
			Assert.AreEqual(Lucene.Net.QueryParsers.QueryParser.AND_OPERATOR, qp.GetDefaultOperator());
			qp.SetDefaultOperator(Lucene.Net.QueryParsers.QueryParser.OR_OPERATOR);
			Assert.AreEqual(Lucene.Net.QueryParsers.QueryParser.OR_OPERATOR, qp.GetDefaultOperator());
		}
		
		[Test]
		public virtual void  TestPunct()
		{
			Analyzer a = new WhitespaceAnalyzer();
			AssertQueryEquals("a&b", a, "a&b");
			AssertQueryEquals("a&&b", a, "a&&b");
			AssertQueryEquals(".NET", a, ".NET");
		}
		
		[Test]
		public virtual void  TestSlop()
		{
			AssertQueryEquals("\"term germ\"~2", null, "\"term germ\"~2");
			AssertQueryEquals("\"term germ\"~2 flork", null, "\"term germ\"~2 flork");
			AssertQueryEquals("\"term\"~2", null, "term");
			AssertQueryEquals("\" \"~2 germ", null, "germ");
			AssertQueryEquals("\"term germ\"~2^2", null, "\"term germ\"~2^2.0");
		}
		
		[Test]
		public virtual void  TestNumber()
		{
			// The numbers go away because SimpleAnalzyer ignores them
			AssertQueryEquals("3", null, "");
			AssertQueryEquals("term 1.0 1 2", null, "term");
			AssertQueryEquals("term term1 term2", null, "term term term");
			
			Analyzer a = new StandardAnalyzer();
			AssertQueryEquals("3", a, "3");
			AssertQueryEquals("term 1.0 1 2", a, "term 1.0 1 2");
			AssertQueryEquals("term term1 term2", a, "term term1 term2");
		}
		
		[Test]
		public virtual void  TestWildcard()
		{
			AssertQueryEquals("term*", null, "term*");
			AssertQueryEquals("term*^2", null, "term*^2.0");
			AssertQueryEquals("term~", null, "term~0.5");
			AssertQueryEquals("term~0.7", null, "term~0.7");
			AssertQueryEquals("term~^2", null, "term~0.5^2.0");
			AssertQueryEquals("term^2~", null, "term~0.5^2.0");
			AssertQueryEquals("term*germ", null, "term*germ");
			AssertQueryEquals("term*germ^3", null, "term*germ^3.0");
			
			Assert.IsTrue(GetQuery("term*", null) is PrefixQuery);
			Assert.IsTrue(GetQuery("term*^2", null) is PrefixQuery);
			Assert.IsTrue(GetQuery("term~", null) is FuzzyQuery);
			Assert.IsTrue(GetQuery("term~0.7", null) is FuzzyQuery);
			FuzzyQuery fq = (FuzzyQuery) GetQuery("term~0.7", null);
			Assert.AreEqual(0.7f, fq.GetMinSimilarity(), 0.1f);
			Assert.AreEqual(FuzzyQuery.defaultPrefixLength, fq.GetPrefixLength());
			fq = (FuzzyQuery) GetQuery("term~", null);
			Assert.AreEqual(0.5f, fq.GetMinSimilarity(), 0.1f);
			Assert.AreEqual(FuzzyQuery.defaultPrefixLength, fq.GetPrefixLength());
			
			AssertParseException("term~1.1"); // value > 1, throws exception
			
			Assert.IsTrue(GetQuery("term*germ", null) is WildcardQuery);
			
			/* Tests to see that wild card terms are (or are not) properly
			* lower-cased with propery parser configuration
			*/
			// First prefix queries:
			// by default, convert to lowercase:
			AssertWildcardQueryEquals("Term*", true, "term*");
			// explicitly set lowercase:
			AssertWildcardQueryEquals("term*", true, "term*");
			AssertWildcardQueryEquals("Term*", true, "term*");
			AssertWildcardQueryEquals("TERM*", true, "term*");
			// explicitly disable lowercase conversion:
			AssertWildcardQueryEquals("term*", false, "term*");
			AssertWildcardQueryEquals("Term*", false, "Term*");
			AssertWildcardQueryEquals("TERM*", false, "TERM*");
			// Then 'full' wildcard queries:
			// by default, convert to lowercase:
			AssertWildcardQueryEquals("Te?m", "te?m");
			// explicitly set lowercase:
			AssertWildcardQueryEquals("te?m", true, "te?m");
			AssertWildcardQueryEquals("Te?m", true, "te?m");
			AssertWildcardQueryEquals("TE?M", true, "te?m");
			AssertWildcardQueryEquals("Te?m*gerM", true, "te?m*germ");
			// explicitly disable lowercase conversion:
			AssertWildcardQueryEquals("te?m", false, "te?m");
			AssertWildcardQueryEquals("Te?m", false, "Te?m");
			AssertWildcardQueryEquals("TE?M", false, "TE?M");
			AssertWildcardQueryEquals("Te?m*gerM", false, "Te?m*gerM");
			//  Fuzzy queries:
			AssertWildcardQueryEquals("Term~", "term~0.5");
			AssertWildcardQueryEquals("Term~", true, "term~0.5");
			AssertWildcardQueryEquals("Term~", false, "Term~0.5");
			//  Range queries:
			AssertWildcardQueryEquals("[A TO C]", "[a TO c]");
			AssertWildcardQueryEquals("[A TO C]", true, "[a TO c]");
			AssertWildcardQueryEquals("[A TO C]", false, "[A TO C]");
			// Test suffix queries: first disallow
			try
			{
				AssertWildcardQueryEquals("*Term", true, "*term");
				Assert.Fail();
			}
			catch (ParseException)
			{
				// expected exception
			}
			try
			{
				AssertWildcardQueryEquals("?Term", true, "?term");
				Assert.Fail();
			}
			catch (ParseException)
			{
				// expected exception
			}
			// Test suffix queries: then allow
			AssertWildcardQueryEquals("*Term", true, "*term", true);
			AssertWildcardQueryEquals("?Term", true, "?term", true);
		}
		
		[Test]
		public virtual void  TestLeadingWildcardType()
		{
			Lucene.Net.QueryParsers.QueryParser qp = GetParser(null);
			qp.SetAllowLeadingWildcard(true);
			Assert.AreEqual(typeof(WildcardQuery), qp.Parse("t*erm*").GetType());
			Assert.AreEqual(typeof(WildcardQuery), qp.Parse("?term*").GetType());
			Assert.AreEqual(typeof(WildcardQuery), qp.Parse("*term*").GetType());
		}
		
		[Test]
		public virtual void  TestQPA()
		{
			AssertQueryEquals("term term^3.0 term", qpAnalyzer, "term term^3.0 term");
			AssertQueryEquals("term stop^3.0 term", qpAnalyzer, "term term");
			
			AssertQueryEquals("term term term", qpAnalyzer, "term term term");
			AssertQueryEquals("term +stop term", qpAnalyzer, "term term");
			AssertQueryEquals("term -stop term", qpAnalyzer, "term term");
			
			AssertQueryEquals("drop AND (stop) AND roll", qpAnalyzer, "+drop +roll");
			AssertQueryEquals("term +(stop) term", qpAnalyzer, "term term");
			AssertQueryEquals("term -(stop) term", qpAnalyzer, "term term");
			
			AssertQueryEquals("drop AND stop AND roll", qpAnalyzer, "+drop +roll");
			AssertQueryEquals("term phrase term", qpAnalyzer, "term \"phrase1 phrase2\" term");
			AssertQueryEquals("term AND NOT phrase term", qpAnalyzer, "+term -\"phrase1 phrase2\" term");
			AssertQueryEquals("stop^3", qpAnalyzer, "");
			AssertQueryEquals("stop", qpAnalyzer, "");
			AssertQueryEquals("(stop)^3", qpAnalyzer, "");
			AssertQueryEquals("((stop))^3", qpAnalyzer, "");
			AssertQueryEquals("(stop^3)", qpAnalyzer, "");
			AssertQueryEquals("((stop)^3)", qpAnalyzer, "");
			AssertQueryEquals("(stop)", qpAnalyzer, "");
			AssertQueryEquals("((stop))", qpAnalyzer, "");
			Assert.IsTrue(GetQuery("term term term", qpAnalyzer) is BooleanQuery);
			Assert.IsTrue(GetQuery("term +stop", qpAnalyzer) is TermQuery);
		}
		
		[Test]
		public virtual void  TestRange()
		{
			AssertQueryEquals("[ a TO z]", null, "[a TO z]");
			Assert.IsTrue(GetQuery("[ a TO z]", null) is ConstantScoreRangeQuery);
			
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new SimpleAnalyzer());
			qp.SetUseOldRangeQuery(true);
			Assert.IsTrue(qp.Parse("[ a TO z]") is RangeQuery);
			
			AssertQueryEquals("[ a TO z ]", null, "[a TO z]");
			AssertQueryEquals("{ a TO z}", null, "{a TO z}");
			AssertQueryEquals("{ a TO z }", null, "{a TO z}");
			AssertQueryEquals("{ a TO z }^2.0", null, "{a TO z}^2.0");
			AssertQueryEquals("[ a TO z] OR bar", null, "[a TO z] bar");
			AssertQueryEquals("[ a TO z] AND bar", null, "+[a TO z] +bar");
			AssertQueryEquals("( bar blar { a TO z}) ", null, "bar blar {a TO z}");
			AssertQueryEquals("gack ( bar blar { a TO z}) ", null, "gack (bar blar {a TO z})");
		}

        public void testFarsiRangeCollating()
        {

            RAMDirectory ramDir = new RAMDirectory();
            IndexWriter iw = new IndexWriter(ramDir, new WhitespaceAnalyzer(), true,
                                             IndexWriter.MaxFieldLength.LIMITED);
            Document doc = new Document();
            doc.Add(new Field("content", "\u0633\u0627\u0628",
                              Field.Store.YES, Field.Index.UN_TOKENIZED));
            iw.AddDocument(doc);
            iw.Close();
            IndexSearcher is_Renamed = new IndexSearcher(ramDir);

            QueryParser qp = new QueryParser("content", new WhitespaceAnalyzer());

            // Neither Java 1.4.2 nor 1.5.0 has Farsi Locale collation available in
            // RuleBasedCollator.  However, the Arabic Locale seems to order the Farsi
            // characters properly.
            System.Globalization.CompareInfo c = new System.Globalization.CultureInfo("ar").CompareInfo;
            qp.SetRangeCollator(c);

            // Unicode order would include U+0633 in [ U+062F - U+0698 ], but Farsi
            // orders the U+0698 character before the U+0633 character, so the single
            // index Term below should NOT be returned by a ConstantScoreRangeQuery
            // with a Farsi Collator (or an Arabic one for the case when Farsi is_Renamed not
            // supported).

            // Test ConstantScoreRangeQuery
            qp.SetUseOldRangeQuery(false);
            ScoreDoc[] result = is_Renamed.Search(qp.Parse("[ \u062F TO \u0698 ]"), null, 1000).scoreDocs;
            Assert.AreEqual(0, result.Length, "The index Term should not be included.");

            result = is_Renamed.Search(qp.Parse("[ \u0633 TO \u0638 ]"), null, 1000).scoreDocs;
            Assert.AreEqual(1, result.Length, "The index Term should be included.");

            // Test RangeQuery
            qp.SetUseOldRangeQuery(true);
            result = is_Renamed.Search(qp.Parse("[ \u062F TO \u0698 ]"), null, 1000).scoreDocs;
            Assert.AreEqual(0, result.Length, "The index Term should not be included.");

            result = is_Renamed.Search(qp.Parse("[ \u0633 TO \u0638 ]"), null, 1000).scoreDocs;
            Assert.AreEqual(1, result.Length, "The index Term should be included.");

            is_Renamed.Close();
        }
  
		/// <summary>for testing legacy DateField support </summary>
		private System.String GetLegacyDate(System.String s)
		{
			System.DateTime tempAux = System.DateTime.Parse(s, System.Globalization.CultureInfo.CurrentCulture);
			return DateField.DateToString(tempAux);
		}
		
		/// <summary>for testing DateTools support </summary>
		private System.String GetDate(System.String s, DateTools.Resolution resolution)
		{
			System.DateTime tempAux = System.DateTime.Parse(s, System.Globalization.CultureInfo.CurrentCulture);
			return GetDate(tempAux, resolution);
		}                                               
		
		/// <summary>for testing DateTools support </summary>
		private System.String GetDate(System.DateTime d, DateTools.Resolution resolution)
		{
			if (resolution == null)
			{
				return DateField.DateToString(d);
			}
			else
			{
				return DateTools.DateToString(d, resolution);
			}
		}
		
		public virtual System.String GetDate(System.String s)
		{
			System.DateTime tempAux = System.DateTime.Parse(s);
			return DateField.DateToString(tempAux);
		}
		
		private System.String GetLocalizedDate(int year, int month, int day, bool extendLastDate)
		{
			System.DateTime temp = new System.DateTime(year, month, day);
			if (extendLastDate)
			{
				temp = temp.AddHours(23);
				temp = temp.AddMinutes(59);
				temp = temp.AddSeconds(59);
				temp = temp.AddMilliseconds(999);
			}
			return temp.ToShortDateString();    // ToString("MM/d/yyy");
		}
		
		/// <summary>for testing legacy DateField support </summary>
		[Test]
		public virtual void  TestLegacyDateRange()
		{
			System.String startDate = GetLocalizedDate(2002, 2, 1, false);
			System.String endDate = GetLocalizedDate(2002, 2, 4, false);
			System.Globalization.Calendar endDateExpected = new System.Globalization.GregorianCalendar();
			// endDateExpected should be set to: "2002, 1, 4, 23, 59, 59, 999" otherwise what's the point of useing GregorianCalendar()   // {{Aroush-2.1}}
			System.DateTime tempAux = new System.DateTime(2002, 2, 4, 23, 59, 59, 999);
			AssertQueryEquals("[ " + startDate + " TO " + endDate + "]", null, "[" + GetLegacyDate(startDate) + " TO " + DateField.DateToString(tempAux) + "]");
			AssertQueryEquals("{  " + startDate + "    " + endDate + "   }", null, "{" + GetLegacyDate(startDate) + " TO " + GetLegacyDate(endDate) + "}");
		}
		
		[Test]
		public virtual void  TestDateRange()
		{
			System.String startDate = GetLocalizedDate(2002, 2, 1, false);
			System.String endDate = GetLocalizedDate(2002, 2, 4, false);
			System.DateTime endDateExpected = new System.DateTime(2002, 2, 4, 23, 59, 59, 999);
			System.String defaultField = "default";
			System.String monthField = "month";
			System.String hourField = "hour";
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new SimpleAnalyzer());
			
			// Don't set any date resolution and verify if DateField is used
			System.DateTime tempAux = endDateExpected;
			AssertDateRangeQueryEquals(qp, defaultField, startDate, endDate, tempAux, null);
			
			// set a field specific date resolution
			qp.SetDateResolution(monthField, DateTools.Resolution.MONTH);
			
			// DateField should still be used for defaultField
			System.DateTime tempAux2 = endDateExpected;
			AssertDateRangeQueryEquals(qp, defaultField, startDate, endDate, tempAux2, null);
			
			// set default date resolution to MILLISECOND 
			qp.SetDateResolution(DateTools.Resolution.MILLISECOND);
			
			// set second field specific date resolution    
			qp.SetDateResolution(hourField, DateTools.Resolution.HOUR);
			
			// for this field no field specific date resolution has been set,
			// so verify if the default resolution is used
			System.DateTime tempAux3 = endDateExpected;
			AssertDateRangeQueryEquals(qp, defaultField, startDate, endDate, tempAux3, DateTools.Resolution.MILLISECOND);
			
			// verify if field specific date resolutions are used for these two fields
			System.DateTime tempAux4 = endDateExpected;
			AssertDateRangeQueryEquals(qp, monthField, startDate, endDate, tempAux4, DateTools.Resolution.MONTH);
			
			System.DateTime tempAux5 = endDateExpected;
			AssertDateRangeQueryEquals(qp, hourField, startDate, endDate, tempAux5, DateTools.Resolution.HOUR);
		}
		
		public virtual void  AssertDateRangeQueryEquals(Lucene.Net.QueryParsers.QueryParser qp, System.String field, System.String startDate, System.String endDate, System.DateTime endDateInclusive, DateTools.Resolution resolution)
		{
			AssertQueryEquals(qp, field, field + ":[" + startDate + " TO " + endDate + "]", "[" + GetDate(startDate, resolution) + " TO " + GetDate(endDateInclusive, resolution) + "]");
			AssertQueryEquals(qp, field, field + ":{" + startDate + " TO " + endDate + "}", "{" + GetDate(startDate, resolution) + " TO " + GetDate(endDate, resolution) + "}");
		}
		
		[Test]
		public virtual void  TestEscaped()
		{
			Analyzer a = new WhitespaceAnalyzer();
			
			/*assertQueryEquals("\\[brackets", a, "\\[brackets");
			assertQueryEquals("\\[brackets", null, "brackets");
			assertQueryEquals("\\\\", a, "\\\\");
			assertQueryEquals("\\+blah", a, "\\+blah");
			assertQueryEquals("\\(blah", a, "\\(blah");
			
			assertQueryEquals("\\-blah", a, "\\-blah");
			assertQueryEquals("\\!blah", a, "\\!blah");
			assertQueryEquals("\\{blah", a, "\\{blah");
			assertQueryEquals("\\}blah", a, "\\}blah");
			assertQueryEquals("\\:blah", a, "\\:blah");
			assertQueryEquals("\\^blah", a, "\\^blah");
			assertQueryEquals("\\[blah", a, "\\[blah");
			assertQueryEquals("\\]blah", a, "\\]blah");
			assertQueryEquals("\\\"blah", a, "\\\"blah");
			assertQueryEquals("\\(blah", a, "\\(blah");
			assertQueryEquals("\\)blah", a, "\\)blah");
			assertQueryEquals("\\~blah", a, "\\~blah");
			assertQueryEquals("\\*blah", a, "\\*blah");
			assertQueryEquals("\\?blah", a, "\\?blah");
			//assertQueryEquals("foo \\&\\& bar", a, "foo \\&\\& bar");
			//assertQueryEquals("foo \\|| bar", a, "foo \\|| bar");
			//assertQueryEquals("foo \\AND bar", a, "foo \\AND bar");*/
			
			AssertQueryEquals("\\a", a, "a");
			
			AssertQueryEquals("a\\-b:c", a, "a-b:c");
			AssertQueryEquals("a\\+b:c", a, "a+b:c");
			AssertQueryEquals("a\\:b:c", a, "a:b:c");
			AssertQueryEquals("a\\\\b:c", a, "a\\b:c");
			
			AssertQueryEquals("a:b\\-c", a, "a:b-c");
			AssertQueryEquals("a:b\\+c", a, "a:b+c");
			AssertQueryEquals("a:b\\:c", a, "a:b:c");
			AssertQueryEquals("a:b\\\\c", a, "a:b\\c");
			
			AssertQueryEquals("a:b\\-c*", a, "a:b-c*");
			AssertQueryEquals("a:b\\+c*", a, "a:b+c*");
			AssertQueryEquals("a:b\\:c*", a, "a:b:c*");
			
			AssertQueryEquals("a:b\\\\c*", a, "a:b\\c*");
			
			AssertQueryEquals("a:b\\-?c", a, "a:b-?c");
			AssertQueryEquals("a:b\\+?c", a, "a:b+?c");
			AssertQueryEquals("a:b\\:?c", a, "a:b:?c");
			
			AssertQueryEquals("a:b\\\\?c", a, "a:b\\?c");
			
			AssertQueryEquals("a:b\\-c~", a, "a:b-c~0.5");
			AssertQueryEquals("a:b\\+c~", a, "a:b+c~0.5");
			AssertQueryEquals("a:b\\:c~", a, "a:b:c~0.5");
			AssertQueryEquals("a:b\\\\c~", a, "a:b\\c~0.5");
			
			AssertQueryEquals("[ a\\- TO a\\+ ]", null, "[a- TO a+]");
			AssertQueryEquals("[ a\\: TO a\\~ ]", null, "[a: TO a~]");
			AssertQueryEquals("[ a\\\\ TO a\\* ]", null, "[a\\ TO a*]");
			
			AssertQueryEquals("[\"c\\:\\\\temp\\\\\\~foo0.txt\" TO \"c\\:\\\\temp\\\\\\~foo9.txt\"]", a, "[c:\\temp\\~foo0.txt TO c:\\temp\\~foo9.txt]");
			
			AssertQueryEquals("a\\\\\\+b", a, "a\\+b");
			
			AssertQueryEquals("a \\\"b c\\\" d", a, "a \"b c\" d");
			AssertQueryEquals("\"a \\\"b c\\\" d\"", a, "\"a \"b c\" d\"");
			AssertQueryEquals("\"a \\+b c d\"", a, "\"a +b c d\"");
			
			AssertQueryEquals("c\\:\\\\temp\\\\\\~foo.txt", a, "c:\\temp\\~foo.txt");
			
			AssertParseException("XY\\"); // there must be a character after the escape char
			
			// test unicode escaping
			AssertQueryEquals("a\\u0062c", a, "abc");
			AssertQueryEquals("XY\\u005a", a, "XYZ");
			AssertQueryEquals("XY\\u005A", a, "XYZ");
			AssertQueryEquals("\"a \\\\\\u0028\\u0062\\\" c\"", a, "\"a \\(b\" c\"");
			
			AssertParseException("XY\\u005G"); // test non-hex character in escaped unicode sequence
			AssertParseException("XY\\u005"); // test incomplete escaped unicode sequence
			
			// Tests bug LUCENE-800
			AssertQueryEquals("(item:\\\\ item:ABCD\\\\)", a, "item:\\ item:ABCD\\");
			AssertParseException("(item:\\\\ item:ABCD\\\\))"); // unmatched closing paranthesis 
			AssertQueryEquals("\\*", a, "*");
			AssertQueryEquals("\\\\", a, "\\"); // escaped backslash
			
			AssertParseException("\\"); // a backslash must always be escaped

            // LUCENE-1189
            AssertQueryEquals("(\"a\\\\\") or (\"b\")", a, "a\\ or b");
		}
		
		[Test]
		public virtual void  TestQueryStringEscaping()
		{
			Analyzer a = new WhitespaceAnalyzer();
			
			AssertEscapedQueryEquals("a-b:c", a, "a\\-b\\:c");
			AssertEscapedQueryEquals("a+b:c", a, "a\\+b\\:c");
			AssertEscapedQueryEquals("a:b:c", a, "a\\:b\\:c");
			AssertEscapedQueryEquals("a\\b:c", a, "a\\\\b\\:c");
			
			AssertEscapedQueryEquals("a:b-c", a, "a\\:b\\-c");
			AssertEscapedQueryEquals("a:b+c", a, "a\\:b\\+c");
			AssertEscapedQueryEquals("a:b:c", a, "a\\:b\\:c");
			AssertEscapedQueryEquals("a:b\\c", a, "a\\:b\\\\c");
			
			AssertEscapedQueryEquals("a:b-c*", a, "a\\:b\\-c\\*");
			AssertEscapedQueryEquals("a:b+c*", a, "a\\:b\\+c\\*");
			AssertEscapedQueryEquals("a:b:c*", a, "a\\:b\\:c\\*");
			
			AssertEscapedQueryEquals("a:b\\\\c*", a, "a\\:b\\\\\\\\c\\*");
			
			AssertEscapedQueryEquals("a:b-?c", a, "a\\:b\\-\\?c");
			AssertEscapedQueryEquals("a:b+?c", a, "a\\:b\\+\\?c");
			AssertEscapedQueryEquals("a:b:?c", a, "a\\:b\\:\\?c");
			
			AssertEscapedQueryEquals("a:b?c", a, "a\\:b\\?c");
			
			AssertEscapedQueryEquals("a:b-c~", a, "a\\:b\\-c\\~");
			AssertEscapedQueryEquals("a:b+c~", a, "a\\:b\\+c\\~");
			AssertEscapedQueryEquals("a:b:c~", a, "a\\:b\\:c\\~");
			AssertEscapedQueryEquals("a:b\\c~", a, "a\\:b\\\\c\\~");
			
			AssertEscapedQueryEquals("[ a - TO a+ ]", null, "\\[ a \\- TO a\\+ \\]");
			AssertEscapedQueryEquals("[ a : TO a~ ]", null, "\\[ a \\: TO a\\~ \\]");
			AssertEscapedQueryEquals("[ a\\ TO a* ]", null, "\\[ a\\\\ TO a\\* \\]");
			
			// LUCENE-881
			AssertEscapedQueryEquals("|| abc ||", a, "\\|\\| abc \\|\\|");
			AssertEscapedQueryEquals("&& abc &&", a, "\\&\\& abc \\&\\&");
		}
		
		[Test]
		public virtual void  TestTabNewlineCarriageReturn()
		{
			AssertQueryEqualsDOA("+weltbank +worlbank", null, "+weltbank +worlbank");
			
			AssertQueryEqualsDOA("+weltbank\n+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \n+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \n +worlbank", null, "+weltbank +worlbank");
			
			AssertQueryEqualsDOA("+weltbank\r+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \r+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \r +worlbank", null, "+weltbank +worlbank");
			
			AssertQueryEqualsDOA("+weltbank\r\n+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \r\n+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \r\n +worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \r \n +worlbank", null, "+weltbank +worlbank");
			
			AssertQueryEqualsDOA("+weltbank\t+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \t+worlbank", null, "+weltbank +worlbank");
			AssertQueryEqualsDOA("weltbank \t +worlbank", null, "+weltbank +worlbank");
		}
		
		[Test]
		public virtual void  TestSimpleDAO()
		{
			AssertQueryEqualsDOA("term term term", null, "+term +term +term");
			AssertQueryEqualsDOA("term +term term", null, "+term +term +term");
			AssertQueryEqualsDOA("term term +term", null, "+term +term +term");
			AssertQueryEqualsDOA("term +term +term", null, "+term +term +term");
			AssertQueryEqualsDOA("-term term term", null, "-term +term +term");
		}
		
		[Test]
		public virtual void  TestBoost()
		{
			StandardAnalyzer oneStopAnalyzer = new StandardAnalyzer(new System.String[]{"on"});
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", oneStopAnalyzer);
			Query q = qp.Parse("on^1.0");
			Assert.IsNotNull(q);
			q = qp.Parse("\"hello\"^2.0");
			Assert.IsNotNull(q);
			Assert.AreEqual(q.GetBoost(), (float) 2.0, (float) 0.5);
			q = qp.Parse("hello^2.0");
			Assert.IsNotNull(q);
			Assert.AreEqual(q.GetBoost(), (float) 2.0, (float) 0.5);
			q = qp.Parse("\"on\"^1.0");
			Assert.IsNotNull(q);
			
			Lucene.Net.QueryParsers.QueryParser qp2 = new Lucene.Net.QueryParsers.QueryParser("field", new StandardAnalyzer());
			q = qp2.Parse("the^3");
			// "the" is a stop word so the result is an empty query:
			Assert.IsNotNull(q);
			Assert.AreEqual("", q.ToString());
			Assert.AreEqual(1.0f, q.GetBoost(), 0.01f);
		}
		
		public virtual void  AssertParseException(System.String queryString)
		{
			try
			{
				Query q = GetQuery(queryString, null);
			}
			catch (ParseException)
			{
				return ;
			}
			Assert.Fail("ParseException expected, not thrown");
		}
		
		[Test]
		public virtual void  TestException()
		{
            AssertParseException("\"some phrase");
            AssertParseException("(foo bar");
            AssertParseException("foo bar))");
            AssertParseException("field:term:with:colon some more terms");
            AssertParseException("(sub query)^5.0^2.0 plus more");
            AssertParseException("secret AND illegal) AND access:confidential");
		}
		
		[Test]
		public virtual void  TestCustomQueryParserWildcard()
		{
			try
			{
				new QPTestParser("contents", new WhitespaceAnalyzer()).Parse("a?t");
				Assert.Fail("Wildcard queries should not be allowed");
			}
			catch (ParseException)
			{
				// expected exception
			}
		}
		
		[Test]
		public virtual void  TestCustomQueryParserFuzzy()
		{
			try
			{
				new QPTestParser("contents", new WhitespaceAnalyzer()).Parse("xunit~");
				Assert.Fail("Fuzzy queries should not be allowed");
			}
			catch (ParseException)
			{
				// expected exception
			}
		}
		
		[Test]
		public virtual void  TestBooleanQuery()
		{
			BooleanQuery.SetMaxClauseCount(2);
			try
			{
				Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new WhitespaceAnalyzer());
				qp.Parse("one two three");
				Assert.Fail("ParseException expected due to too many bool clauses");
			}
			catch (ParseException)
			{
				// too many bool clauses, so ParseException is expected
			}
		}
		
		/// <summary> This test differs from TestPrecedenceQueryParser</summary>
		[Test]
		public virtual void  TestPrecedence()
		{
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new WhitespaceAnalyzer());
			Query query1 = qp.Parse("A AND B OR C AND D");
			Query query2 = qp.Parse("+A +B +C +D");
			Assert.AreEqual(query1, query2);
		}
		
		[Test]
		public virtual void  TestLocalDateFormat()
		{
			Lucene.Net.Store.RAMDirectory ramDir = new Lucene.Net.Store.RAMDirectory();
			Lucene.Net.Index.IndexWriter iw = new Lucene.Net.Index.IndexWriter(ramDir, new WhitespaceAnalyzer(), true, IndexWriter.MaxFieldLength.LIMITED);
			AddDateDoc("a", 2005, 12, 2, 10, 15, 33, iw);
			AddDateDoc("b", 2005, 12, 4, 22, 15, 0, iw);
			iw.Close();
			Lucene.Net.Search.IndexSearcher is_Renamed = new Lucene.Net.Search.IndexSearcher(ramDir);
			AssertHits(1, "[12/1/2005 TO 12/3/2005]", is_Renamed);
			AssertHits(2, "[12/1/2005 TO 12/4/2005]", is_Renamed);
			AssertHits(1, "[12/3/2005 TO 12/4/2005]", is_Renamed);
			AssertHits(1, "{12/1/2005 TO 12/3/2005}", is_Renamed);
			AssertHits(1, "{12/1/2005 TO 12/4/2005}", is_Renamed);
			AssertHits(0, "{12/3/2005 TO 12/4/2005}", is_Renamed);
			is_Renamed.Close();
		}
		
		[Test]
		public virtual void  TestStarParsing()
		{
			int[] type = new int[1];
			Lucene.Net.QueryParsers.QueryParser qp = new AnonymousClassQueryParser(type, this, "field", new WhitespaceAnalyzer());
			
			TermQuery tq;
			
			tq = (TermQuery) qp.Parse("foo:zoo*");
			Assert.AreEqual("zoo", tq.GetTerm().Text());
			Assert.AreEqual(2, type[0]);
			
			tq = (TermQuery) qp.Parse("foo:zoo*^2");
			Assert.AreEqual("zoo", tq.GetTerm().Text());
			Assert.AreEqual(2, type[0]);
			Assert.AreEqual(tq.GetBoost(), 2, 0);
			
			tq = (TermQuery) qp.Parse("foo:*");
			Assert.AreEqual("*", tq.GetTerm().Text());
			Assert.AreEqual(1, type[0]); // could be a valid prefix query in the future too
			
			tq = (TermQuery) qp.Parse("foo:*^2");
			Assert.AreEqual("*", tq.GetTerm().Text());
			Assert.AreEqual(1, type[0]);
			Assert.AreEqual(tq.GetBoost(), 2, 0);
			
			tq = (TermQuery) qp.Parse("*:foo");
			Assert.AreEqual("*", tq.GetTerm().Field());
			Assert.AreEqual("foo", tq.GetTerm().Text());
			Assert.AreEqual(3, type[0]);
			
			tq = (TermQuery) qp.Parse("*:*");
			Assert.AreEqual("*", tq.GetTerm().Field());
			Assert.AreEqual("*", tq.GetTerm().Text());
			Assert.AreEqual(1, type[0]); // could be handled as a prefix query in the future
			
			tq = (TermQuery) qp.Parse("(*:*)");
			Assert.AreEqual("*", tq.GetTerm().Field());
			Assert.AreEqual("*", tq.GetTerm().Text());
			Assert.AreEqual(1, type[0]);
		}
		
		[Test]
		public virtual void  TestStopwords()
		{
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("a", new StopAnalyzer(new System.String[] { "the", "foo" }));
			Query result = qp.Parse("a:the OR a:foo");
			Assert.IsNotNull(result, "result is null and it shouldn't be");
			Assert.IsTrue(result is BooleanQuery, "result is not a BooleanQuery");
			Assert.IsTrue(((BooleanQuery) result).Clauses().Count == 0, ((BooleanQuery) result).Clauses().Count + " does not equal: " + 0);
			result = qp.Parse("a:woo OR a:the");
			Assert.IsNotNull(result, "result is null and it shouldn't be");
			Assert.IsTrue(result is TermQuery, "result is not a TermQuery");
			result = qp.Parse("(fieldX:xxxxx OR fieldy:xxxxxxxx)^2 AND (fieldx:the OR fieldy:foo)");
			Assert.IsNotNull(result, "result is null and it shouldn't be");
			Assert.IsTrue(result is BooleanQuery, "result is not a BooleanQuery");
			System.Console.Out.WriteLine("Result: " + result);
			Assert.IsTrue(((BooleanQuery) result).Clauses().Count == 2, ((BooleanQuery) result).Clauses().Count + " does not equal: " + 2);
		}
		
		[Test]
		public virtual void  TestPositionIncrement()
		{
			bool dflt = StopFilter.GetEnablePositionIncrementsDefault();
			StopFilter.SetEnablePositionIncrementsDefault(true);
			try
			{
				Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("a", new StopAnalyzer(new System.String[] { "the", "in", "are", "this" }));
				qp.SetEnablePositionIncrements(true);
				System.String qtxt = "\"the words in poisitions pos02578 are stopped in this phrasequery\"";
				//               0         2                      5           7  8
				int[] expectedPositions = new int[]{1, 3, 4, 6, 9};
				PhraseQuery pq = (PhraseQuery) qp.Parse(qtxt);
				//System.out.println("Query text: "+qtxt);
				//System.out.println("Result: "+pq);
				Term[] t = pq.GetTerms();
				int[] pos = pq.GetPositions();
				for (int i = 0; i < t.Length; i++)
				{
					//System.out.println(i+". "+t[i]+"  pos: "+pos[i]);
					Assert.AreEqual(expectedPositions[i], pos[i], "term " + i + " = " + t[i] + " has wrong term-position!");
				}
			}
			finally
			{
				StopFilter.SetEnablePositionIncrementsDefault(dflt);
			}
		}
		
		[Test]
		public virtual void  TestMatchAllDocs()
		{
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("field", new WhitespaceAnalyzer());
			Assert.AreEqual(new MatchAllDocsQuery(), qp.Parse("*:*"));
			Assert.AreEqual(new MatchAllDocsQuery(), qp.Parse("(*:*)"));
			BooleanQuery bq = (BooleanQuery) qp.Parse("+*:* -*:*");
			Assert.IsTrue(bq.GetClauses()[0].GetQuery() is MatchAllDocsQuery);
			Assert.IsTrue(bq.GetClauses()[1].GetQuery() is MatchAllDocsQuery);
		}
		
		private void  AssertHits(int expected, System.String query, Lucene.Net.Search.IndexSearcher is_Renamed)
		{
			Lucene.Net.QueryParsers.QueryParser qp = new Lucene.Net.QueryParsers.QueryParser("date", new WhitespaceAnalyzer());
			qp.SetLocale(new System.Globalization.CultureInfo("en-US"));
			Query q = qp.Parse(query);
			ScoreDoc[] hits = is_Renamed.Search(q, null, 1000).scoreDocs;
			Assert.AreEqual(expected, hits.Length);
		}
		
		private static void  AddDateDoc(System.String content, int year, int month, int day, int hour, int minute, int second, Lucene.Net.Index.IndexWriter iw)
		{
			Lucene.Net.Documents.Document d = new Lucene.Net.Documents.Document();
			d.Add(new Lucene.Net.Documents.Field("f", content, Lucene.Net.Documents.Field.Store.YES, Lucene.Net.Documents.Field.Index.ANALYZED));
			System.DateTime tempAux = new System.DateTime(year, month, day, hour, minute, second);
			d.Add(new Lucene.Net.Documents.Field("date", DateField.DateToString(tempAux), Lucene.Net.Documents.Field.Store.YES, Lucene.Net.Documents.Field.Index.NOT_ANALYZED));
			iw.AddDocument(d);
		}
		
		[TearDown]
		public override void TearDown()
		{
			base.TearDown();
			BooleanQuery.SetMaxClauseCount(originalMaxClauses);
		}
	}
}