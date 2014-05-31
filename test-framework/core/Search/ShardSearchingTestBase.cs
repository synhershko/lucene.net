using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

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


	using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
	using IndexReader = Lucene.Net.Index.IndexReader;
	using IndexWriter = Lucene.Net.Index.IndexWriter;
	using IndexWriterConfig = Lucene.Net.Index.IndexWriterConfig;
	using Term = Lucene.Net.Index.Term;
	using TermContext = Lucene.Net.Index.TermContext;
	using Directory = Lucene.Net.Store.Directory;
	using LineFileDocs = Lucene.Net.Util.LineFileDocs;
	using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
	using PrintStreamInfoStream = Lucene.Net.Util.PrintStreamInfoStream;
	using TestUtil = Lucene.Net.Util.TestUtil;

	// TODO
	//   - doc blocks?  so we can test joins/grouping...
	//   - controlled consistency (NRTMgr)

	/// <summary>
	/// Base test class for simulating distributed search across multiple shards.
	/// </summary>
	public abstract class ShardSearchingTestBase : LuceneTestCase
	{

	  // TODO: maybe SLM should throw this instead of returning null...
	  /// <summary>
	  /// Thrown when the lease for a searcher has expired.
	  /// </summary>
	  public class SearcherExpiredException : Exception
	  {
		public SearcherExpiredException(string message) : base(message)
		{
		}
	  }

	  private class FieldAndShardVersion
	  {
		internal readonly long Version;
		internal readonly int NodeID;
		internal readonly string Field;

		public FieldAndShardVersion(int nodeID, long version, string field)
		{
		  this.NodeID = nodeID;
		  this.Version = version;
		  this.Field = field;
		}

		public override int HashCode()
		{
		  return (int)(Version * NodeID + Field.GetHashCode());
		}

		public override bool Equals(object _other)
		{
		  if (!(_other is FieldAndShardVersion))
		  {
			return false;
		  }

		  FieldAndShardVersion other = (FieldAndShardVersion) _other;

		  return Field.Equals(other.Field) && Version == other.Version && NodeID == other.NodeID;
		}

		public override string ToString()
		{
		  return "FieldAndShardVersion(field=" + Field + " nodeID=" + NodeID + " version=" + Version + ")";
		}
	  }

	  private class TermAndShardVersion
	  {
		internal readonly long Version;
		internal readonly int NodeID;
		internal readonly Term Term;

		public TermAndShardVersion(int nodeID, long version, Term term)
		{
		  this.NodeID = nodeID;
		  this.Version = version;
		  this.Term = term;
		}

		public override int HashCode()
		{
		  return (int)(Version * NodeID + Term.GetHashCode());
		}

		public override bool Equals(object _other)
		{
		  if (!(_other is TermAndShardVersion))
		  {
			return false;
		  }

		  TermAndShardVersion other = (TermAndShardVersion) _other;

		  return Term.Equals(other.Term) && Version == other.Version && NodeID == other.NodeID;
		}
	  }

	  // We share collection stats for these fields on each node
	  // reopen:
	  private readonly string[] FieldsToShare = new string[] {"body", "title"};

	  // Called by one node once it has reopened, to notify all
	  // other nodes.  this is just a mock (since it goes and
	  // directly updates all other nodes, in RAM)... in a real
	  // env this would hit the wire, sending version &
	  // collection stats to all other nodes:
	  internal virtual void BroadcastNodeReopen(int nodeID, long version, IndexSearcher newSearcher)
	  {

		if (VERBOSE)
		{
		  Console.WriteLine("REOPEN: nodeID=" + nodeID + " version=" + version + " maxDoc=" + newSearcher.IndexReader.maxDoc());
		}

		// Broadcast new collection stats for this node to all
		// other nodes:
		foreach (string field in FieldsToShare)
		{
		  CollectionStatistics stats = newSearcher.collectionStatistics(field);
		  foreach (NodeState node in Nodes)
		  {
			// Don't put my own collection stats into the cache;
			// we pull locally:
			if (node.MyNodeID != nodeID)
			{
			  node.CollectionStatsCache[new FieldAndShardVersion(nodeID, version, field)] = stats;
			}
		  }
		}
		foreach (NodeState node in Nodes)
		{
		  node.UpdateNodeVersion(nodeID, version);
		}
	  }

	  // TODO: broadcastNodeExpire?  then we can purge the
	  // known-stale cache entries...

	  // MOCK: in a real env you have to hit the wire
	  // (send this query to all remote nodes
	  // concurrently):
	  internal virtual TopDocs SearchNode(int nodeID, long[] nodeVersions, Query q, Sort sort, int numHits, ScoreDoc searchAfter)
	  {
		NodeState.ShardIndexSearcher s = Nodes[nodeID].Acquire(nodeVersions);
		try
		{
		  if (sort == null)
		  {
			if (searchAfter != null)
			{
			  return s.LocalSearchAfter(searchAfter, q, numHits);
			}
			else
			{
			  return s.LocalSearch(q, numHits);
			}
		  }
		  else
		  {
			Debug.Assert(searchAfter == null); // not supported yet
			return s.LocalSearch(q, numHits, sort);
		  }
		}
		finally
		{
		  Nodes[nodeID].Release(s);
		}
	  }

	  // Mock: in a real env, this would hit the wire and get
	  // term stats from remote node
	  internal virtual IDictionary<Term, TermStatistics> GetNodeTermStats(Set<Term> terms, int nodeID, long version)
	  {
		NodeState node = Nodes[nodeID];
		IDictionary<Term, TermStatistics> stats = new Dictionary<Term, TermStatistics>();
		IndexSearcher s = node.Searchers.acquire(version);
		if (s == null)
		{
		  throw new SearcherExpiredException("node=" + nodeID + " version=" + version);
		}
		try
		{
		  foreach (Term term in terms)
		  {
			TermContext termContext = TermContext.build(s.IndexReader.Context, term);
			stats[term] = s.termStatistics(term, termContext);
		  }
		}
		finally
		{
		  node.Searchers.release(s);
		}
		return stats;
	  }

	  protected internal sealed class NodeState : IDisposable
	  {
		  private readonly ShardSearchingTestBase OuterInstance;

		public readonly Directory Dir;
		public readonly IndexWriter Writer;
		public readonly SearcherLifetimeManager Searchers;
		public readonly SearcherManager Mgr;
		public readonly int MyNodeID;
		public readonly long[] CurrentNodeVersions;

		// TODO: nothing evicts from here!!!  Somehow, on searcher
		// expiration on remote nodes we must evict from our
		// local cache...?  And still LRU otherwise (for the
		// still-live searchers).

		internal readonly IDictionary<FieldAndShardVersion, CollectionStatistics> CollectionStatsCache = new ConcurrentDictionary<FieldAndShardVersion, CollectionStatistics>();
		internal readonly IDictionary<TermAndShardVersion, TermStatistics> TermStatsCache = new ConcurrentDictionary<TermAndShardVersion, TermStatistics>();

		/// <summary>
		/// Matches docs in the local shard but scores based on
		///  aggregated stats ("mock distributed scoring") from all
		///  nodes. 
		/// </summary>

		public class ShardIndexSearcher : IndexSearcher
		{
			private readonly ShardSearchingTestbase.NodeState OuterInstance;

		  // Version for the node searchers we search:
		  public readonly long[] NodeVersions;
		  public readonly int MyNodeID;

		  public ShardIndexSearcher(ShardSearchingTestbase.NodeState outerInstance, long[] nodeVersions, IndexReader localReader, int nodeID) : base(localReader)
		  {
			  this.OuterInstance = outerInstance;
			this.NodeVersions = nodeVersions;
			MyNodeID = nodeID;
			Debug.Assert(MyNodeID == outerInstance.MyNodeID, "myNodeID=" + nodeID + " NodeState.this.myNodeID=" + outerInstance.MyNodeID);
		  }

		  public override Query Rewrite(Query original)
		  {
			Query rewritten = base.rewrite(original);
			Set<Term> terms = new HashSet<Term>();
			rewritten.extractTerms(terms);

			// Make a single request to remote nodes for term
			// stats:
			for (int nodeID = 0;nodeID < NodeVersions.Length;nodeID++)
			{
			  if (nodeID == MyNodeID)
			  {
				continue;
			  }

			  Set<Term> missing = new HashSet<Term>();
			  foreach (Term term in terms)
			  {
				TermAndShardVersion key = new TermAndShardVersion(nodeID, NodeVersions[nodeID], term);
				if (!outerInstance.TermStatsCache.ContainsKey(key))
				{
				  missing.add(term);
				}
			  }
			  if (missing.size() != 0)
			  {
				foreach (KeyValuePair<Term, TermStatistics> ent in outerInstance.outerInstance.GetNodeTermStats(missing, nodeID, NodeVersions[nodeID]))
				{
				  TermAndShardVersion key = new TermAndShardVersion(nodeID, NodeVersions[nodeID], ent.Key);
				  outerInstance.TermStatsCache[key] = ent.Value;
				}
			  }
			}

			return rewritten;
		  }

		  public override TermStatistics TermStatistics(Term term, TermContext context)
		  {
			Debug.Assert(term != null);
			long docFreq = 0;
			long totalTermFreq = 0;
			for (int nodeID = 0;nodeID < NodeVersions.Length;nodeID++)
			{

			  TermStatistics subStats;
			  if (nodeID == MyNodeID)
			  {
				subStats = base.termStatistics(term, context);
			  }
			  else
			  {
				TermAndShardVersion key = new TermAndShardVersion(nodeID, NodeVersions[nodeID], term);
				subStats = outerInstance.TermStatsCache[key];
				// We pre-cached during rewrite so all terms
				// better be here...
				Debug.Assert(subStats != null);
			  }

			  long nodeDocFreq = subStats.docFreq();
			  if (docFreq >= 0 && nodeDocFreq >= 0)
			  {
				docFreq += nodeDocFreq;
			  }
			  else
			  {
				docFreq = -1;
			  }

			  long nodeTotalTermFreq = subStats.totalTermFreq();
			  if (totalTermFreq >= 0 && nodeTotalTermFreq >= 0)
			  {
				totalTermFreq += nodeTotalTermFreq;
			  }
			  else
			  {
				totalTermFreq = -1;
			  }
			}

			return new TermStatistics(term.bytes(), docFreq, totalTermFreq);
		  }

		  public override CollectionStatistics CollectionStatistics(string field)
		  {
			// TODO: we could compute this on init and cache,
			// since we are re-inited whenever any nodes have a
			// new reader
			long docCount = 0;
			long sumTotalTermFreq = 0;
			long sumDocFreq = 0;
			long maxDoc = 0;

			for (int nodeID = 0;nodeID < NodeVersions.Length;nodeID++)
			{
			  FieldAndShardVersion key = new FieldAndShardVersion(nodeID, NodeVersions[nodeID], field);
			  CollectionStatistics nodeStats;
			  if (nodeID == MyNodeID)
			  {
				nodeStats = base.collectionStatistics(field);
			  }
			  else
			  {
				nodeStats = outerInstance.CollectionStatsCache[key];
			  }
			  if (nodeStats == null)
			  {
				Console.WriteLine("coll stats myNodeID=" + MyNodeID + ": " + outerInstance.CollectionStatsCache.Keys);
			  }
			  // Collection stats are pre-shared on reopen, so,
			  // we better not have a cache miss:
			  Debug.Assert(nodeStats != null, "myNodeID=" + MyNodeID + " nodeID=" + nodeID + " version=" + NodeVersions[nodeID] + " field=" + field);

			  long nodeDocCount = nodeStats.docCount();
			  if (docCount >= 0 && nodeDocCount >= 0)
			  {
				docCount += nodeDocCount;
			  }
			  else
			  {
				docCount = -1;
			  }

			  long nodeSumTotalTermFreq = nodeStats.sumTotalTermFreq();
			  if (sumTotalTermFreq >= 0 && nodeSumTotalTermFreq >= 0)
			  {
				sumTotalTermFreq += nodeSumTotalTermFreq;
			  }
			  else
			  {
				sumTotalTermFreq = -1;
			  }

			  long nodeSumDocFreq = nodeStats.sumDocFreq();
			  if (sumDocFreq >= 0 && nodeSumDocFreq >= 0)
			  {
				sumDocFreq += nodeSumDocFreq;
			  }
			  else
			  {
				sumDocFreq = -1;
			  }

			  Debug.Assert(nodeStats.maxDoc() >= 0);
			  maxDoc += nodeStats.maxDoc();
			}

			return new CollectionStatistics(field, maxDoc, docCount, sumTotalTermFreq, sumDocFreq);
		  }

		  public override TopDocs Search(Query query, int numHits)
		  {
			TopDocs[] shardHits = new TopDocs[NodeVersions.Length];
			for (int nodeID = 0;nodeID < NodeVersions.Length;nodeID++)
			{
			  if (nodeID == MyNodeID)
			  {
				// My node; run using local shard searcher we
				// already aquired:
				shardHits[nodeID] = LocalSearch(query, numHits);
			  }
			  else
			  {
				shardHits[nodeID] = outerInstance.outerInstance.SearchNode(nodeID, NodeVersions, query, null, numHits, null);
			  }
			}

			// Merge:
			return TopDocs.merge(null, numHits, shardHits);
		  }

		  public virtual TopDocs LocalSearch(Query query, int numHits)
		  {
			return base.search(query, numHits);
		  }

		  public override TopDocs SearchAfter(ScoreDoc after, Query query, int numHits)
		  {
			TopDocs[] shardHits = new TopDocs[NodeVersions.Length];
			// results are merged in that order: score, shardIndex, doc. therefore we set
			// after to after.score and depending on the nodeID we set doc to either:
			// - not collect any more documents with that score (only with worse score)
			// - collect more documents with that score (and worse) following the last collected document
			// - collect all documents with that score (and worse)
			ScoreDoc shardAfter = new ScoreDoc(after.doc, after.score);
			for (int nodeID = 0; nodeID < NodeVersions.Length; nodeID++)
			{
			  if (nodeID < after.shardIndex)
			  {
				// all documents with after.score were already collected, so collect
				// only documents with worse scores.
				NodeState.ShardIndexSearcher s = outerInstance.OuterInstance.Nodes[nodeID].acquire(NodeVersions);
				try
				{
				  // Setting after.doc to reader.maxDoc-1 is a way to tell
				  // TopScoreDocCollector that no more docs with that score should
				  // be collected. note that in practice the shard which sends the
				  // request to a remote shard won't have reader.maxDoc at hand, so
				  // it will send some arbitrary value which will be fixed on the
				  // other end.
				  shardAfter.doc = s.IndexReader.maxDoc() - 1;
				}
				finally
				{
				  outerInstance.OuterInstance.Nodes[nodeID].release(s);
				}
			  }
			  else if (nodeID == after.shardIndex)
			  {
				// collect all documents following the last collected doc with
				// after.score + documents with worse scores.  
				shardAfter.doc = after.doc;
			  }
			  else
			  {
				// all documents with after.score (and worse) should be collected
				// because they didn't make it to top-N in the previous round.
				shardAfter.doc = -1;
			  }
			  if (nodeID == MyNodeID)
			  {
				// My node; run using local shard searcher we
				// already aquired:
				shardHits[nodeID] = LocalSearchAfter(shardAfter, query, numHits);
			  }
			  else
			  {
				shardHits[nodeID] = outerInstance.outerInstance.SearchNode(nodeID, NodeVersions, query, null, numHits, shardAfter);
			  }
			  //System.out.println("  node=" + nodeID + " totHits=" + shardHits[nodeID].totalHits);
			}

			// Merge:
			return TopDocs.merge(null, numHits, shardHits);
		  }

		  public virtual TopDocs LocalSearchAfter(ScoreDoc after, Query query, int numHits)
		  {
			return base.searchAfter(after, query, numHits);
		  }

		  public override TopFieldDocs Search(Query query, int numHits, Sort sort)
		  {
			Debug.Assert(sort != null);
			TopDocs[] shardHits = new TopDocs[NodeVersions.Length];
			for (int nodeID = 0;nodeID < NodeVersions.Length;nodeID++)
			{
			  if (nodeID == MyNodeID)
			  {
				// My node; run using local shard searcher we
				// already aquired:
				shardHits[nodeID] = LocalSearch(query, numHits, sort);
			  }
			  else
			  {
				shardHits[nodeID] = outerInstance.outerInstance.SearchNode(nodeID, NodeVersions, query, sort, numHits, null);
			  }
			}

			// Merge:
			return (TopFieldDocs) TopDocs.merge(sort, numHits, shardHits);
		  }

		  public virtual TopFieldDocs LocalSearch(Query query, int numHits, Sort sort)
		  {
			return base.search(query, numHits, sort);
		  }

		}

		internal volatile ShardIndexSearcher CurrentShardSearcher;

		public NodeState(ShardSearchingTestBase outerInstance, Random random, int nodeID, int numNodes)
		{
			this.OuterInstance = outerInstance;
		  MyNodeID = nodeID;
		  Dir = NewFSDirectory(CreateTempDir("ShardSearchingTestBase"));
		  // TODO: set warmer
		  MockAnalyzer analyzer = new MockAnalyzer(Random());
		  analyzer.MaxTokenLength = TestUtil.NextInt(Random(), 1, IndexWriter.MAX_TERM_LENGTH);
		  IndexWriterConfig iwc = new IndexWriterConfig(TEST_VERSION_CURRENT, analyzer);
		  iwc.OpenMode_e = IndexWriterConfig.OpenMode_e.CREATE;
		  if (VERBOSE)
		  {
			iwc.InfoStream = new PrintStreamInfoStream(System.out);
		  }
		  Writer = new IndexWriter(Dir, iwc);
		  Mgr = new SearcherManager(Writer, true, null);
		  Searchers = new SearcherLifetimeManager();

		  // Init w/ 0s... caller above will do initial
		  // "broadcast" by calling initSearcher:
		  CurrentNodeVersions = new long[numNodes];
		}

		public void InitSearcher(long[] nodeVersions)
		{
		  Debug.Assert(CurrentShardSearcher == null);
		  Array.Copy(nodeVersions, 0, CurrentNodeVersions, 0, CurrentNodeVersions.Length);
		  CurrentShardSearcher = new ShardIndexSearcher(this, CurrentNodeVersions.clone(), Mgr.acquire().IndexReader, MyNodeID);
		}

		public void UpdateNodeVersion(int nodeID, long version)
		{
		  CurrentNodeVersions[nodeID] = version;
		  if (CurrentShardSearcher != null)
		  {
			CurrentShardSearcher.IndexReader.decRef();
		  }
		  CurrentShardSearcher = new ShardIndexSearcher(this, CurrentNodeVersions.clone(), Mgr.acquire().IndexReader, MyNodeID);
		}

		// Get the current (fresh) searcher for this node
		public ShardIndexSearcher Acquire()
		{
		  while (true)
		  {
			ShardIndexSearcher s = CurrentShardSearcher;
			// In theory the reader could get decRef'd to 0
			// before we have a chance to incRef, ie if a reopen
			// happens right after the above line, this thread
			// gets stalled, and the old IR is closed.  So we
			// must try/retry until incRef succeeds:
			if (s.IndexReader.tryIncRef())
			{
			  return s;
			}
		  }
		}

		public void Release(ShardIndexSearcher s)
		{
		  s.IndexReader.decRef();
		}

		// Get and old searcher matching the specified versions:
		public ShardIndexSearcher Acquire(long[] nodeVersions)
		{
		  IndexSearcher s = Searchers.acquire(nodeVersions[MyNodeID]);
		  if (s == null)
		  {
			throw new SearcherExpiredException("nodeID=" + MyNodeID + " version=" + nodeVersions[MyNodeID]);
		  }
		  return new ShardIndexSearcher(this, nodeVersions, s.IndexReader, MyNodeID);
		}

		// Reopen local reader
		public void Reopen()
		{
		  IndexSearcher before = Mgr.acquire();
		  Mgr.release(before);

		  Mgr.maybeRefresh();
		  IndexSearcher after = Mgr.acquire();
		  try
		  {
			if (after != before)
			{
			  // New searcher was opened
			  long version = Searchers.record(after);
			  Searchers.prune(new SearcherLifetimeManager.PruneByAge(outerInstance.MaxSearcherAgeSeconds));
			  outerInstance.BroadcastNodeReopen(MyNodeID, version, after);
			}
		  }
		  finally
		  {
			Mgr.release(after);
		  }
		}

		public override void Close()
		{
		  if (CurrentShardSearcher != null)
		  {
			CurrentShardSearcher.IndexReader.decRef();
		  }
		  Searchers.close();
		  Mgr.close();
		  Writer.close();
		  Dir.close();
		}
	  }

	  // TODO: make this more realistic, ie, each node should
	  // have its own thread, so we have true node to node
	  // concurrency
	  private sealed class ChangeIndices : System.Threading.Thread
	  {
		  private readonly ShardSearchingTestBase OuterInstance;

		  public ChangeIndices(ShardSearchingTestBase outerInstance)
		  {
			  this.OuterInstance = outerInstance;
		  }

		public override void Run()
		{
		  try
		  {
			LineFileDocs docs = new LineFileDocs(Random(), DefaultCodecSupportsDocValues());
			int numDocs = 0;
			while (System.nanoTime() < outerInstance.EndTimeNanos)
			{
			  int what = Random().Next(3);
			  NodeState node = outerInstance.Nodes[Random().Next(outerInstance.Nodes.Length)];
			  if (numDocs == 0 || what == 0)
			  {
				node.Writer.addDocument(docs.NextDoc());
				numDocs++;
			  }
			  else if (what == 1)
			  {
				node.Writer.updateDocument(new Term("docid", "" + Random().Next(numDocs)), docs.NextDoc());
				numDocs++;
			  }
			  else
			  {
				node.Writer.deleteDocuments(new Term("docid", "" + Random().Next(numDocs)));
			  }
			  // TODO: doc blocks too

			  if (Random().Next(17) == 12)
			  {
				node.Writer.commit();
			  }

			  if (Random().Next(17) == 12)
			  {
				outerInstance.Nodes[Random().Next(outerInstance.Nodes.Length)].Reopen();
			  }
			}
		  }
		  catch (Exception t)
		  {
			Console.WriteLine("FAILED:");
			t.printStackTrace(System.out);
			throw new Exception(t);
		  }
		}
	  }

	  protected internal NodeState[] Nodes;
	  internal int MaxSearcherAgeSeconds;
	  internal long EndTimeNanos;
	  private Thread ChangeIndicesThread;

	  protected internal virtual void Start(int numNodes, double runTimeSec, int maxSearcherAgeSeconds)
	  {

		EndTimeNanos = System.nanoTime() + (long)(runTimeSec * 1000000000);
		this.MaxSearcherAgeSeconds = maxSearcherAgeSeconds;

		Nodes = new NodeState[numNodes];
		for (int nodeID = 0;nodeID < numNodes;nodeID++)
		{
		  Nodes[nodeID] = new NodeState(this, Random(), nodeID, numNodes);
		}

		long[] nodeVersions = new long[Nodes.Length];
		for (int nodeID = 0;nodeID < numNodes;nodeID++)
		{
		  IndexSearcher s = Nodes[nodeID].Mgr.acquire();
		  try
		  {
			nodeVersions[nodeID] = Nodes[nodeID].Searchers.record(s);
		  }
		  finally
		  {
			Nodes[nodeID].Mgr.release(s);
		  }
		}

		for (int nodeID = 0;nodeID < numNodes;nodeID++)
		{
		  IndexSearcher s = Nodes[nodeID].Mgr.acquire();
		  Debug.Assert(nodeVersions[nodeID] == Nodes[nodeID].Searchers.record(s));
		  Debug.Assert(s != null);
		  try
		  {
			BroadcastNodeReopen(nodeID, nodeVersions[nodeID], s);
		  }
		  finally
		  {
			Nodes[nodeID].Mgr.release(s);
		  }
		}

		ChangeIndicesThread = new ChangeIndices(this);
		ChangeIndicesThread.Start();
	  }

	  protected internal virtual void Finish()
	  {
		ChangeIndicesThread.Join();
		foreach (NodeState node in Nodes)
		{
		  node.Close();
		}
	  }

	  /// <summary>
	  /// An IndexSearcher and associated version (lease)
	  /// </summary>
	  protected internal class SearcherAndVersion
	  {
		public readonly IndexSearcher Searcher;
		public readonly long Version;

		public SearcherAndVersion(IndexSearcher searcher, long version)
		{
		  this.Searcher = searcher;
		  this.Version = version;
		}
	  }
	}

}