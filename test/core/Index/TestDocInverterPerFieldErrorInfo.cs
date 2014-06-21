using System;

namespace Lucene.Net.Index
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

	using Analyzer = Lucene.Net.Analysis.Analyzer;
	using MockTokenizer = Lucene.Net.Analysis.MockTokenizer;
	using TokenFilter = Lucene.Net.Analysis.TokenFilter;
	using Tokenizer = Lucene.Net.Analysis.Tokenizer;
	using Document = Lucene.Net.Document.Document;
	using FieldType = Lucene.Net.Document.FieldType;
	using TextField = Lucene.Net.Document.TextField;
	using Directory = Lucene.Net.Store.Directory;
	using IOUtils = Lucene.Net.Util.IOUtils;
	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
	using PrintStreamInfoStream = Lucene.Net.Util.PrintStreamInfoStream;
    using NUnit.Framework;
    using System.IO;


	/// <summary>
	/// Test adding to the info stream when there's an exception thrown during field analysis.
	/// </summary>
	public class TestDocInverterPerFieldErrorInfo : LuceneTestCase
	{
	  private static readonly FieldType StoredTextType = new FieldType(TextField.TYPE_NOT_STORED);

	  private class BadNews : Exception
	  {
		internal BadNews(string message) : base(message)
		{
		}
	  }

	  private class ThrowingAnalyzer : Analyzer
	  {
		protected override TokenStreamComponents CreateComponents(string fieldName, TextReader input)
		{
		  Tokenizer tokenizer = new MockTokenizer(input);
		  if (fieldName.Equals("distinctiveFieldName"))
		  {
			TokenFilter tosser = new TokenFilterAnonymousInnerClassHelper(this, tokenizer);
			return new TokenStreamComponents(tokenizer, tosser);
		  }
		  else
		  {
			return new TokenStreamComponents(tokenizer);
		  }
		}

		private class TokenFilterAnonymousInnerClassHelper : TokenFilter
		{
			private readonly ThrowingAnalyzer OuterInstance;

			public TokenFilterAnonymousInnerClassHelper(ThrowingAnalyzer outerInstance, Tokenizer tokenizer) : base(tokenizer)
			{
				this.OuterInstance = outerInstance;
			}

			public override bool IncrementToken()
			{
			  throw new BadNews("Something is icky.");
			}
		}
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testInfoStreamGetsFieldName() throws Exception
	  public virtual void TestInfoStreamGetsFieldName()
	  {
		Directory dir = NewDirectory();
		IndexWriter writer;
		IndexWriterConfig c = new IndexWriterConfig(TEST_VERSION_CURRENT, new ThrowingAnalyzer());
		ByteArrayOutputStream infoBytes = new ByteArrayOutputStream();
		PrintStream infoPrintStream = new PrintStream(infoBytes, true, IOUtils.UTF_8);
		PrintStreamInfoStream printStreamInfoStream = new PrintStreamInfoStream(infoPrintStream);
		c.InfoStream = printStreamInfoStream;
		writer = new IndexWriter(dir, c);
		Document doc = new Document();
		doc.Add(NewField("distinctiveFieldName", "aaa ", StoredTextType));
		try
		{
		  writer.AddDocument(doc);
		  Assert.Fail("Failed to fail.");
		}
		catch (BadNews badNews)
		{
		  infoPrintStream.Flush();
		  string infoStream = new string(infoBytes.toByteArray(), IOUtils.UTF_8);
		  Assert.IsTrue(infoStream.Contains("distinctiveFieldName"));
		}

		writer.Dispose();
		dir.Dispose();
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Test public void testNoExtraNoise() throws Exception
	  public virtual void TestNoExtraNoise()
	  {
		Directory dir = NewDirectory();
		IndexWriter writer;
		IndexWriterConfig c = new IndexWriterConfig(TEST_VERSION_CURRENT, new ThrowingAnalyzer());
		ByteArrayOutputStream infoBytes = new ByteArrayOutputStream();
		PrintStream infoPrintStream = new PrintStream(infoBytes, true, IOUtils.UTF_8);
		PrintStreamInfoStream printStreamInfoStream = new PrintStreamInfoStream(infoPrintStream);
		c.InfoStream = printStreamInfoStream;
		writer = new IndexWriter(dir, c);
		Document doc = new Document();
		doc.Add(NewField("boringFieldName", "aaa ", StoredTextType));
		try
		{
		  writer.AddDocument(doc);
		}
		catch (BadNews badNews)
		{
		  Assert.Fail("Unwanted exception");
		}
		infoPrintStream.Flush();
		string infoStream = new string(infoBytes.toByteArray(), IOUtils.UTF_8);
		Assert.IsFalse(infoStream.Contains("boringFieldName"));

		writer.Dispose();
		dir.Dispose();
	  }

	}

}