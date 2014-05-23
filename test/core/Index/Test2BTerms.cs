using System;
using System.Collections.Generic;

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

	using Lucene.Net.Util;
	using SuppressCodecs = Lucene.Net.Util.LuceneTestCase.SuppressCodecs;
	using Lucene.Net.Store;
	using Lucene.Net.Search;
	using Lucene.Net.Analysis;
	using Lucene.Net.Analysis.Tokenattributes;
	using Lucene.Net.Document;
	using IndexOptions = Lucene.Net.Index.FieldInfo.IndexOptions_e;
	using Ignore = org.junit.Ignore;


	using Lucene.Net.Analysis;
	using Lucene.Net.Analysis.Tokenattributes;
	using Codec = Lucene.Net.Codecs.Codec;
	using Lucene.Net.Document;
	using IndexOptions = Lucene.Net.Index.FieldInfo.IndexOptions_e;
	using Lucene.Net.Search;
	using Lucene.Net.Store;
	using Lucene.Net.Util;
	using SuppressCodecs = Lucene.Net.Util.LuceneTestCase.SuppressCodecs;

	// NOTE: this test will fail w/ PreFlexRW codec!  (Because
	// this test uses full binary term space, but PreFlex cannot
	// handle this since it requires the terms are UTF8 bytes).
	//
	// Also, SimpleText codec will consume very large amounts of
	// disk (but, should run successfully).  Best to run w/
	// -Dtests.codec=Standard, and w/ plenty of RAM, eg:
	//
	//   ant test -Dtest.slow=true -Dtests.heapsize=8g
	//
	//   java -server -Xmx8g -d64 -cp .:lib/junit-4.10.jar:./build/classes/test:./build/classes/test-framework:./build/classes/java -Dlucene.version=4.0-dev -Dtests.directory=MMapDirectory -DtempDir=build -ea org.junit.runner.JUnitCore Lucene.Net.Index.Test2BTerms
	//
//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @SuppressCodecs({ "SimpleText", "Memory", "Direct" }) public class Test2BTerms extends LuceneTestCase
	public class Test2BTerms : LuceneTestCase
	{

	  private const int TOKEN_LEN = 5;

	  private static readonly BytesRef Bytes = new BytesRef(TOKEN_LEN);

	  private sealed class MyTokenStream : TokenStream
	  {

		internal readonly int TokensPerDoc;
		internal int TokenCount;
		public readonly IList<BytesRef> SavedTerms = new List<BytesRef>();
		internal int NextSave;
		internal long TermCounter;
		internal readonly Random Random;

		public MyTokenStream(Random random, int tokensPerDoc) : base(new MyAttributeFactory(AttributeFactory.DEFAULT_ATTRIBUTE_FACTORY))
		{
		  this.TokensPerDoc = tokensPerDoc;
		  addAttribute(typeof(TermToBytesRefAttribute));
		  Bytes.length = TOKEN_LEN;
		  this.Random = random;
		  NextSave = TestUtil.Next(random, 500000, 1000000);
		}

		public override bool IncrementToken()
		{
		  ClearAttributes();
		  if (TokenCount >= TokensPerDoc)
		  {
			return false;
		  }
		  int shift = 32;
		  for (int i = 0;i < 5;i++)
		  {
			Bytes.bytes[i] = unchecked((sbyte)((TermCounter >> shift) & 0xFF));
			shift -= 8;
		  }
		  TermCounter++;
		  TokenCount++;
		  if (--NextSave == 0)
		  {
			SavedTerms.Add(BytesRef.deepCopyOf(Bytes));
			Console.WriteLine("TEST: save term=" + Bytes);
			NextSave = TestUtil.Next(Random, 500000, 1000000);
		  }
		  return true;
		}

		public override void Reset()
		{
		  TokenCount = 0;
		}

		private sealed class MyTermAttributeImpl : AttributeImpl, TermToBytesRefAttribute
		{
		  public override void FillBytesRef()
		  {
			// no-op: the bytes was already filled by our owner's incrementToken
		  }

		  public override BytesRef BytesRef
		  {
			  get
			  {
				return Bytes;
			  }
		  }

		  public override void Clear()
		  {
		  }

		  public override bool Equals(object other)
		  {
			return other == this;
		  }

		  public override int HashCode()
		  {
			return System.identityHashCode(this);
		  }

		  public override void CopyTo(AttributeImpl target)
		  {
		  }

		  public override MyTermAttributeImpl Clone()
		  {
			throw new System.NotSupportedException();
		  }
		}

		private sealed class MyAttributeFactory : AttributeFactory
		{
		  internal readonly AttributeFactory @delegate;

		  public MyAttributeFactory(AttributeFactory @delegate)
		  {
			this.@delegate = @delegate;
		  }

		  public override AttributeImpl CreateAttributeInstance(Type attClass)
		  {
			if (attClass == typeof(TermToBytesRefAttribute))
			{
			  return new MyTermAttributeImpl();
			}
			if (attClass.IsSubclassOf(typeof(CharTermAttribute)))
			{
			  throw new System.ArgumentException("no");
			}
			return @delegate.createAttributeInstance(attClass);
		  }
		}
	  }

//JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
//ORIGINAL LINE: @Ignore("Very slow. Enable manually by removing @Ignore.") public void test2BTerms() throws java.io.IOException
	  public virtual void Test2BTerms()
	  {

		if ("Lucene3x".Equals(Codec.Default.Name))
		{
		  throw new Exception("this test cannot run with PreFlex codec");
		}
		Console.WriteLine("Starting Test2B");
		long TERM_COUNT = ((long) int.MaxValue) + 100000000;

		int TERMS_PER_DOC = TestUtil.Next(random(), 100000, 1000000);

		IList<BytesRef> savedTerms = null;

		BaseDirectoryWrapper dir = newFSDirectory(createTempDir("2BTerms"));
		//MockDirectoryWrapper dir = newFSDirectory(new File("/p/lucene/indices/2bindex"));
		if (dir is MockDirectoryWrapper)
		{
		  ((MockDirectoryWrapper)dir).Throttling = MockDirectoryWrapper.Throttling.NEVER;
		}
		dir.CheckIndexOnClose = false; // don't double-checkindex

		if (true)
		{

		  IndexWriter w = new IndexWriter(dir, new IndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(random()))
									 .setMaxBufferedDocs(IndexWriterConfig.DISABLE_AUTO_FLUSH).setRAMBufferSizeMB(256.0).setMergeScheduler(new ConcurrentMergeScheduler()).setMergePolicy(newLogMergePolicy(false, 10)).setOpenMode(IndexWriterConfig.OpenMode.CREATE));

		  MergePolicy mp = w.Config.MergePolicy;
		  if (mp is LogByteSizeMergePolicy)
		  {
			// 1 petabyte:
			((LogByteSizeMergePolicy) mp).MaxMergeMB = 1024 * 1024 * 1024;
		  }

		  Document doc = new Document();
		  MyTokenStream ts = new MyTokenStream(random(), TERMS_PER_DOC);

		  FieldType customType = new FieldType(TextField.TYPE_NOT_STORED);
		  customType.IndexOptions = IndexOptions.DOCS_ONLY;
		  customType.OmitNorms = true;
		  Field field = new Field("field", ts, customType);
		  doc.add(field);
		  //w.setInfoStream(System.out);
		  int numDocs = (int)(TERM_COUNT / TERMS_PER_DOC);

		  Console.WriteLine("TERMS_PER_DOC=" + TERMS_PER_DOC);
		  Console.WriteLine("numDocs=" + numDocs);

		  for (int i = 0;i < numDocs;i++)
		  {
			long t0 = System.currentTimeMillis();
			w.addDocument(doc);
			Console.WriteLine(i + " of " + numDocs + " " + (System.currentTimeMillis() - t0) + " msec");
		  }
		  savedTerms = ts.SavedTerms;

		  Console.WriteLine("TEST: full merge");
		  w.forceMerge(1);
		  Console.WriteLine("TEST: close writer");
		  w.close();
		}

		Console.WriteLine("TEST: open reader");
		IndexReader r = DirectoryReader.open(dir);
		if (savedTerms == null)
		{
		  savedTerms = FindTerms(r);
		}
		int numSavedTerms = savedTerms.Count;
		IList<BytesRef> bigOrdTerms = new List<BytesRef>(savedTerms.subList(numSavedTerms - 10, numSavedTerms));
		Console.WriteLine("TEST: test big ord terms...");
		TestSavedTerms(r, bigOrdTerms);
		Console.WriteLine("TEST: test all saved terms...");
		TestSavedTerms(r, savedTerms);
		r.close();

		Console.WriteLine("TEST: now CheckIndex...");
		CheckIndex.Status status = TestUtil.checkIndex(dir);
		long tc = status.segmentInfos.get(0).termIndexStatus.termCount;
		Assert.IsTrue("count " + tc + " is not > " + int.MaxValue, tc > int.MaxValue);

		dir.close();
		Console.WriteLine("TEST: done!");
	  }

	  private IList<BytesRef> FindTerms(IndexReader r)
	  {
		Console.WriteLine("TEST: findTerms");
		TermsEnum termsEnum = MultiFields.getTerms(r, "field").iterator(null);
		IList<BytesRef> savedTerms = new List<BytesRef>();
		int nextSave = TestUtil.Next(random(), 500000, 1000000);
		BytesRef term;
		while ((term = termsEnum.next()) != null)
		{
		  if (--nextSave == 0)
		  {
			savedTerms.Add(BytesRef.deepCopyOf(term));
			Console.WriteLine("TEST: add " + term);
			nextSave = TestUtil.Next(random(), 500000, 1000000);
		  }
		}
		return savedTerms;
	  }

	  private void TestSavedTerms(IndexReader r, IList<BytesRef> terms)
	  {
		Console.WriteLine("TEST: run " + terms.Count + " terms on reader=" + r);
		IndexSearcher s = newSearcher(r);
		Collections.shuffle(terms);
		TermsEnum termsEnum = MultiFields.getTerms(r, "field").iterator(null);
		bool failed = false;
		for (int iter = 0;iter < 10 * terms.Count;iter++)
		{
		  BytesRef term = terms[random().Next(terms.Count)];
		  Console.WriteLine("TEST: search " + term);
		  long t0 = System.currentTimeMillis();
		  int count = s.search(new TermQuery(new Term("field", term)), 1).totalHits;
		  if (count <= 0)
		  {
			Console.WriteLine("  FAILED: count=" + count);
			failed = true;
		  }
		  long t1 = System.currentTimeMillis();
		  Console.WriteLine("  took " + (t1 - t0) + " millis");

		  TermsEnum.SeekStatus result = termsEnum.seekCeil(term);
		  if (result != TermsEnum.SeekStatus.FOUND)
		  {
			if (result == TermsEnum.SeekStatus.END)
			{
			  Console.WriteLine("  FAILED: got END");
			}
			else
			{
			  Console.WriteLine("  FAILED: wrong term: got " + termsEnum.term());
			}
			failed = true;
		  }
		}
		Assert.IsFalse(failed);
	  }
	}

}