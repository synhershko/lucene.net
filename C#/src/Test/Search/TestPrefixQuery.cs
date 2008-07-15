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

using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using Term = Lucene.Net.Index.Term;
using RAMDirectory = Lucene.Net.Store.RAMDirectory;
using WhitespaceAnalyzer = Lucene.Net.Analysis.WhitespaceAnalyzer;
using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

namespace Lucene.Net.Search
{
	
	/// <summary> Tests {@link PrefixQuery} class.
	/// 
	/// </summary>
	/// <author>  Erik Hatcher
	/// </author>
	[TestFixture]
	public class TestPrefixQuery : LuceneTestCase
	{
		[Test]
		public virtual void  TestPrefixQuery_Renamed_Method()
		{
			RAMDirectory directory = new RAMDirectory();
			
			System.String[] categories = new System.String[]{"/Computers", "/Computers/Mac", "/Computers/Windows"};
			IndexWriter writer = new IndexWriter(directory, new WhitespaceAnalyzer(), true);
			for (int i = 0; i < categories.Length; i++)
			{
				Lucene.Net.Documents.Document doc = new Lucene.Net.Documents.Document();
				doc.Add(new Field("category", categories[i], Field.Store.YES, Field.Index.UN_TOKENIZED));
				writer.AddDocument(doc);
			}
			writer.Close();
			
			PrefixQuery query = new PrefixQuery(new Term("category", "/Computers"));
			IndexSearcher searcher = new IndexSearcher(directory);
			Hits hits = searcher.Search(query);
			Assert.AreEqual(3, hits.Length(), "All documents in /Computers category and below");
			
			query = new PrefixQuery(new Term("category", "/Computers/Mac"));
			hits = searcher.Search(query);
			Assert.AreEqual(1, hits.Length(), "One in /Computers/Mac");
		}
	}
}