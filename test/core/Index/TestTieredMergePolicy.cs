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

	using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
	using Document = Lucene.Net.Document.Document;
	using Field = Lucene.Net.Document.Field;
	using Directory = Lucene.Net.Store.Directory;
	using TestUtil = Lucene.Net.Util.TestUtil;
    using NUnit.Framework;

	public class TestTieredMergePolicy : BaseMergePolicyTestCase
	{

	  protected internal override MergePolicy MergePolicy()
	  {
		return NewTieredMergePolicy();
	  }

	  public virtual void TestForceMergeDeletes()
	  {
		Directory dir = NewDirectory();
		IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
		TieredMergePolicy tmp = NewTieredMergePolicy();
		conf.SetMergePolicy(tmp);
		conf.SetMaxBufferedDocs(4);
		tmp.MaxMergeAtOnce = 100;
		tmp.SegmentsPerTier = 100;
		tmp.ForceMergeDeletesPctAllowed = 30.0;
		IndexWriter w = new IndexWriter(dir, conf);
		for (int i = 0;i < 80;i++)
		{
		  Document doc = new Document();
		  doc.Add(NewTextField("content", "aaa " + (i % 4), Field.Store.NO));
		  w.AddDocument(doc);
		}
		Assert.AreEqual(80, w.MaxDoc());
		Assert.AreEqual(80, w.NumDocs());

		if (VERBOSE)
		{
		  Console.WriteLine("\nTEST: delete docs");
		}
		w.DeleteDocuments(new Term("content", "0"));
		w.ForceMergeDeletes();

		Assert.AreEqual(80, w.MaxDoc());
		Assert.AreEqual(60, w.NumDocs());

		if (VERBOSE)
		{
		  Console.WriteLine("\nTEST: forceMergeDeletes2");
		}
		((TieredMergePolicy) w.Config.MergePolicy).ForceMergeDeletesPctAllowed = 10.0;
		w.ForceMergeDeletes();
		Assert.AreEqual(60, w.MaxDoc());
		Assert.AreEqual(60, w.NumDocs());
		w.Dispose();
		dir.Dispose();
	  }

	  public virtual void TestPartialMerge()
	  {
		int num = AtLeast(10);
		for (int iter = 0;iter < num;iter++)
		{
		  if (VERBOSE)
		  {
			Console.WriteLine("TEST: iter=" + iter);
		  }
		  Directory dir = NewDirectory();
		  IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
		  conf.SetMergeScheduler(new SerialMergeScheduler());
		  TieredMergePolicy tmp = NewTieredMergePolicy();
		  conf.SetMergePolicy(tmp);
		  conf.SetMaxBufferedDocs(2);
		  tmp.MaxMergeAtOnce = 3;
		  tmp.SegmentsPerTier = 6;

		  IndexWriter w = new IndexWriter(dir, conf);
		  int maxCount = 0;
		  int numDocs = TestUtil.NextInt(Random(), 20, 100);
		  for (int i = 0;i < numDocs;i++)
		  {
			Document doc = new Document();
			doc.Add(NewTextField("content", "aaa " + (i % 4), Field.Store.NO));
			w.AddDocument(doc);
			int count = w.SegmentCount;
			maxCount = Math.Max(count, maxCount);
			Assert.IsTrue(count >= maxCount - 3, "count=" + count + " maxCount=" + maxCount);
		  }

		  w.Flush(true, true);

		  int segmentCount = w.SegmentCount;
		  int targetCount = TestUtil.NextInt(Random(), 1, segmentCount);
		  if (VERBOSE)
		  {
			Console.WriteLine("TEST: merge to " + targetCount + " segs (current count=" + segmentCount + ")");
		  }
		  w.ForceMerge(targetCount);
		  Assert.AreEqual(targetCount, w.SegmentCount);

		  w.Dispose();
		  dir.Dispose();
		}
	  }

	  public virtual void TestForceMergeDeletesMaxSegSize()
	  {
		Directory dir = NewDirectory();
		IndexWriterConfig conf = NewIndexWriterConfig(TEST_VERSION_CURRENT, new MockAnalyzer(Random()));
		TieredMergePolicy tmp = new TieredMergePolicy();
		tmp.MaxMergedSegmentMB = 0.01;
		tmp.ForceMergeDeletesPctAllowed = 0.0;
		conf.SetMergePolicy(tmp);

		RandomIndexWriter w = new RandomIndexWriter(Random(), dir, conf);
		w.DoRandomForceMerge = false;

		int numDocs = AtLeast(200);
		for (int i = 0;i < numDocs;i++)
		{
		  Document doc = new Document();
		  doc.Add(NewStringField("id", "" + i, Field.Store.NO));
		  doc.Add(NewTextField("content", "aaa " + i, Field.Store.NO));
		  w.AddDocument(doc);
		}

		w.ForceMerge(1);
		IndexReader r = w.Reader;
		Assert.AreEqual(numDocs, r.MaxDoc());
		Assert.AreEqual(numDocs, r.NumDocs());
		r.Dispose();

		if (VERBOSE)
		{
		  Console.WriteLine("\nTEST: delete doc");
		}

		w.DeleteDocuments(new Term("id", "" + (42 + 17)));

		r = w.Reader;
		Assert.AreEqual(numDocs, r.MaxDoc());
		Assert.AreEqual(numDocs - 1, r.NumDocs());
		r.Dispose();

		w.ForceMergeDeletes();

		r = w.Reader;
		Assert.AreEqual(numDocs - 1, r.MaxDoc());
		Assert.AreEqual(numDocs - 1, r.NumDocs());
		r.Dispose();

        w.Close();

		dir.Dispose();
	  }

	  private const double EPSILON = 1E-14;

	  public virtual void TestSetters()
	  {
		TieredMergePolicy tmp = new TieredMergePolicy();

		tmp.MaxMergedSegmentMB = 0.5;
		Assert.AreEqual(0.5, tmp.MaxMergedSegmentMB, EPSILON);

		tmp.MaxMergedSegmentMB = double.PositiveInfinity;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.MaxMergedSegmentMB, EPSILON * long.MaxValue);

		tmp.MaxMergedSegmentMB = long.MaxValue / 1024 / 1024.0;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.MaxMergedSegmentMB, EPSILON * long.MaxValue);

		try
		{
		  tmp.MaxMergedSegmentMB = -2.0;
		  Assert.Fail("Didn't throw IllegalArgumentException");
		}
		catch (System.ArgumentException iae)
		{
		  // pass
		}

		tmp.FloorSegmentMB = 2.0;
		Assert.AreEqual(2.0, tmp.FloorSegmentMB, EPSILON);

		tmp.FloorSegmentMB = double.PositiveInfinity;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.FloorSegmentMB, EPSILON * long.MaxValue);

		tmp.FloorSegmentMB = long.MaxValue / 1024 / 1024.0;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.FloorSegmentMB, EPSILON * long.MaxValue);

		try
		{
		  tmp.FloorSegmentMB = -2.0;
		  Assert.Fail("Didn't throw IllegalArgumentException");
		}
		catch (System.ArgumentException iae)
		{
		  // pass
		}

		tmp.MaxCFSSegmentSizeMB = 2.0;
		Assert.AreEqual(2.0, tmp.MaxCFSSegmentSizeMB, EPSILON);

		tmp.MaxCFSSegmentSizeMB = double.PositiveInfinity;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.MaxCFSSegmentSizeMB, EPSILON * long.MaxValue);

		tmp.MaxCFSSegmentSizeMB = long.MaxValue / 1024 / 1024.0;
		Assert.AreEqual(long.MaxValue / 1024 / 1024.0, tmp.MaxCFSSegmentSizeMB, EPSILON * long.MaxValue);

		try
		{
		  tmp.MaxCFSSegmentSizeMB = -2.0;
		  Assert.Fail("Didn't throw IllegalArgumentException");
		}
		catch (System.ArgumentException iae)
		{
		  // pass
		}

		// TODO: Add more checks for other non-double setters!
	  }
	}

}