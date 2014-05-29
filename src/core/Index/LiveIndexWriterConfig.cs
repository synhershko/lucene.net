using System.Text;

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
	using Codec = Lucene.Net.Codecs.Codec;
	using Lucene41PostingsFormat = Lucene.Net.Codecs.Lucene41.Lucene41PostingsFormat; // javadocs
	using IndexingChain = Lucene.Net.Index.DocumentsWriterPerThread.IndexingChain;
	using IndexReaderWarmer = Lucene.Net.Index.IndexWriter.IndexReaderWarmer;
	using OpenMode = Lucene.Net.Index.IndexWriterConfig.OpenMode;
	using IndexSearcher = Lucene.Net.Search.IndexSearcher;
	using Similarity = Lucene.Net.Search.Similarities.Similarity;
	using InfoStream = Lucene.Net.Util.InfoStream;
	using Version = Lucene.Net.Util.Version;

	/// <summary>
	/// Holds all the configuration used by <seealso cref="IndexWriter"/> with few setters for
	/// settings that can be changed on an <seealso cref="IndexWriter"/> instance "live".
	/// 
	/// @since 4.0
	/// </summary>
	public class LiveIndexWriterConfig
	{

	  private readonly Analyzer Analyzer_Renamed;

	  private volatile int MaxBufferedDocs_Renamed;
	  private volatile double RamBufferSizeMB;
	  private volatile int MaxBufferedDeleteTerms_Renamed;
	  private volatile int ReaderTermsIndexDivisor_Renamed;
	  private volatile IndexReaderWarmer MergedSegmentWarmer_Renamed;
	  private volatile int TermIndexInterval_Renamed; // TODO: this should be private to the codec, not settable here

	  // modified by IndexWriterConfig
	  /// <summary>
	  /// <seealso cref="IndexDeletionPolicy"/> controlling when commit
	  ///  points are deleted. 
	  /// </summary>
	  protected internal volatile IndexDeletionPolicy DelPolicy;

	  /// <summary>
	  /// <seealso cref="IndexCommit"/> that <seealso cref="IndexWriter"/> is
	  ///  opened on. 
	  /// </summary>
	  protected internal volatile IndexCommit Commit;

	  /// <summary>
	  /// <seealso cref="OpenMode"/> that <seealso cref="IndexWriter"/> is opened
	  ///  with. 
	  /// </summary>
	  protected internal volatile OpenMode OpenMode_Renamed;

	  /// <summary>
	  /// <seealso cref="Similarity"/> to use when encoding norms. </summary>
	  protected internal volatile Similarity Similarity_Renamed;

	  /// <summary>
	  /// <seealso cref="MergeScheduler"/> to use for running merges. </summary>
	  protected internal volatile MergeScheduler MergeScheduler_Renamed;

	  /// <summary>
	  /// Timeout when trying to obtain the write lock on init. </summary>
	  protected internal volatile long WriteLockTimeout_Renamed;

	  /// <summary>
	  /// <seealso cref="IndexingChain"/> that determines how documents are
	  ///  indexed. 
	  /// </summary>
	  protected internal volatile IndexingChain IndexingChain_Renamed;

	  /// <summary>
	  /// <seealso cref="Codec"/> used to write new segments. </summary>
	  protected internal volatile Codec Codec_Renamed;

	  /// <summary>
	  /// <seealso cref="InfoStream"/> for debugging messages. </summary>
	  protected internal volatile InfoStream InfoStream_Renamed;

	  /// <summary>
	  /// <seealso cref="MergePolicy"/> for selecting merges. </summary>
	  protected internal volatile MergePolicy MergePolicy_Renamed;

	  /// <summary>
	  /// {@code DocumentsWriterPerThreadPool} to control how
	  ///  threads are allocated to {@code DocumentsWriterPerThread}. 
	  /// </summary>
	  protected internal volatile DocumentsWriterPerThreadPool IndexerThreadPool_Renamed;

	  /// <summary>
	  /// True if readers should be pooled. </summary>
	  protected internal volatile bool ReaderPooling_Renamed;

	  /// <summary>
	  /// <seealso cref="FlushPolicy"/> to control when segments are
	  ///  flushed. 
	  /// </summary>
	  protected internal volatile FlushPolicy FlushPolicy_Renamed;

	  /// <summary>
	  /// Sets the hard upper bound on RAM usage for a single
	  ///  segment, after which the segment is forced to flush. 
	  /// </summary>
	  protected internal volatile int PerThreadHardLimitMB;

	  /// <summary>
	  /// <seealso cref="Version"/> that <seealso cref="IndexWriter"/> should emulate. </summary>
	  protected internal readonly Version MatchVersion;

	  /// <summary>
	  /// True if segment flushes should use compound file format </summary>
	  protected internal volatile bool UseCompoundFile_Renamed = IndexWriterConfig.DEFAULT_USE_COMPOUND_FILE_SYSTEM;

	  /// <summary>
	  /// True if merging should check integrity of segments before merge </summary>
	  protected internal volatile bool CheckIntegrityAtMerge_Renamed = IndexWriterConfig.DEFAULT_CHECK_INTEGRITY_AT_MERGE;

	  // used by IndexWriterConfig
	  internal LiveIndexWriterConfig(Analyzer analyzer, Version matchVersion)
	  {
		this.Analyzer_Renamed = analyzer;
		this.MatchVersion = matchVersion;
		RamBufferSizeMB = IndexWriterConfig.DEFAULT_RAM_BUFFER_SIZE_MB;
		MaxBufferedDocs_Renamed = IndexWriterConfig.DEFAULT_MAX_BUFFERED_DOCS;
		MaxBufferedDeleteTerms_Renamed = IndexWriterConfig.DEFAULT_MAX_BUFFERED_DELETE_TERMS;
		ReaderTermsIndexDivisor_Renamed = IndexWriterConfig.DEFAULT_READER_TERMS_INDEX_DIVISOR;
		MergedSegmentWarmer_Renamed = null;
		TermIndexInterval_Renamed = IndexWriterConfig.DEFAULT_TERM_INDEX_INTERVAL; // TODO: this should be private to the codec, not settable here
		DelPolicy = new KeepOnlyLastCommitDeletionPolicy();
		Commit = null;
		UseCompoundFile_Renamed = IndexWriterConfig.DEFAULT_USE_COMPOUND_FILE_SYSTEM;
		OpenMode_Renamed = OpenMode.CREATE_OR_APPEND;
		Similarity_Renamed = IndexSearcher.DefaultSimilarity;
		MergeScheduler_Renamed = new ConcurrentMergeScheduler();
		WriteLockTimeout_Renamed = IndexWriterConfig.WRITE_LOCK_TIMEOUT;
		IndexingChain_Renamed = DocumentsWriterPerThread.defaultIndexingChain;
		Codec_Renamed = Codec.Default;
		if (Codec_Renamed == null)
		{
		  throw new System.NullReferenceException();
		}
		InfoStream_Renamed = InfoStream.Default;
		MergePolicy_Renamed = new TieredMergePolicy();
		FlushPolicy_Renamed = new FlushByRamOrCountsPolicy();
		ReaderPooling_Renamed = IndexWriterConfig.DEFAULT_READER_POOLING;
		IndexerThreadPool_Renamed = new ThreadAffinityDocumentsWriterThreadPool(IndexWriterConfig.DEFAULT_MAX_THREAD_STATES);
		PerThreadHardLimitMB = IndexWriterConfig.DEFAULT_RAM_PER_THREAD_HARD_LIMIT_MB;
	  }

	  /// <summary>
	  /// Creates a new config that that handles the live <seealso cref="IndexWriter"/>
	  /// settings.
	  /// </summary>
	  internal LiveIndexWriterConfig(IndexWriterConfig config)
	  {
		MaxBufferedDeleteTerms_Renamed = config.MaxBufferedDeleteTerms;
		MaxBufferedDocs_Renamed = config.MaxBufferedDocs;
		MergedSegmentWarmer_Renamed = config.MergedSegmentWarmer;
		RamBufferSizeMB = config.RAMBufferSizeMB;
		ReaderTermsIndexDivisor_Renamed = config.ReaderTermsIndexDivisor;
		TermIndexInterval_Renamed = config.TermIndexInterval;
		MatchVersion = config.MatchVersion;
		Analyzer_Renamed = config.Analyzer;
		DelPolicy = config.IndexDeletionPolicy;
		Commit = config.IndexCommit;
		OpenMode_Renamed = config.OpenMode;
		Similarity_Renamed = config.Similarity;
		MergeScheduler_Renamed = config.MergeScheduler;
		WriteLockTimeout_Renamed = config.WriteLockTimeout;
		IndexingChain_Renamed = config.IndexingChain;
		Codec_Renamed = config.Codec;
		InfoStream_Renamed = config.InfoStream;
		MergePolicy_Renamed = config.MergePolicy;
		IndexerThreadPool_Renamed = config.IndexerThreadPool;
		ReaderPooling_Renamed = config.ReaderPooling;
		FlushPolicy_Renamed = config.FlushPolicy;
		PerThreadHardLimitMB = config.RAMPerThreadHardLimitMB;
		UseCompoundFile_Renamed = config.UseCompoundFile;
		CheckIntegrityAtMerge_Renamed = config.CheckIntegrityAtMerge;
	  }

	  /// <summary>
	  /// Returns the default analyzer to use for indexing documents. </summary>
	  public virtual Analyzer Analyzer
	  {
		  get
		  {
			return Analyzer_Renamed;
		  }
	  }

	  /// <summary>
	  /// Expert: set the interval between indexed terms. Large values cause less
	  /// memory to be used by IndexReader, but slow random-access to terms. Small
	  /// values cause more memory to be used by an IndexReader, and speed
	  /// random-access to terms.
	  /// <p>
	  /// this parameter determines the amount of computation required per query
	  /// term, regardless of the number of documents that contain that term. In
	  /// particular, it is the maximum number of other terms that must be scanned
	  /// before a term is located and its frequency and position information may be
	  /// processed. In a large index with user-entered query terms, query processing
	  /// time is likely to be dominated not by term lookup but rather by the
	  /// processing of frequency and positional data. In a small index or when many
	  /// uncommon query terms are generated (e.g., by wildcard queries) term lookup
	  /// may become a dominant cost.
	  /// <p>
	  /// In particular, <code>numUniqueTerms/interval</code> terms are read into
	  /// memory by an IndexReader, and, on average, <code>interval/2</code> terms
	  /// must be scanned for each random term access.
	  /// 
	  /// <p>
	  /// Takes effect immediately, but only applies to newly flushed/merged
	  /// segments.
	  /// 
	  /// <p>
	  /// <b>NOTE:</b> this parameter does not apply to all PostingsFormat implementations,
	  /// including the default one in this release. It only makes sense for term indexes
	  /// that are implemented as a fixed gap between terms. For example, 
	  /// <seealso cref="Lucene41PostingsFormat"/> implements the term index instead based upon how
	  /// terms share prefixes. To configure its parameters (the minimum and maximum size
	  /// for a block), you would instead use  <seealso cref="Lucene41PostingsFormat#Lucene41PostingsFormat(int, int)"/>.
	  /// which can also be configured on a per-field basis:
	  /// <pre class="prettyprint">
	  /// //customize Lucene41PostingsFormat, passing minBlockSize=50, maxBlockSize=100
	  /// final PostingsFormat tweakedPostings = new Lucene41PostingsFormat(50, 100);
	  /// iwc.setCodec(new Lucene45Codec() {
	  ///   &#64;Override
	  ///   public PostingsFormat getPostingsFormatForField(String field) {
	  ///     if (field.equals("fieldWithTonsOfTerms"))
	  ///       return tweakedPostings;
	  ///     else
	  ///       return super.getPostingsFormatForField(field);
	  ///   }
	  /// });
	  /// </pre>
	  /// Note that other implementations may have their own parameters, or no parameters at all.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#DEFAULT_TERM_INDEX_INTERVAL </seealso>
	  public virtual LiveIndexWriterConfig SetTermIndexInterval(int interval) // TODO: this should be private to the codec, not settable here
	  {
		this.TermIndexInterval_Renamed = interval;
		return this;
	  }

	  /// <summary>
	  /// Returns the interval between indexed terms.
	  /// </summary>
	  /// <seealso cref= #setTermIndexInterval(int) </seealso>
	  public virtual int TermIndexInterval
	  {
		  get
		  {
			return TermIndexInterval_Renamed;
		  }
	  }

	  /// <summary>
	  /// Determines the maximum number of delete-by-term operations that will be
	  /// buffered before both the buffered in-memory delete terms and queries are
	  /// applied and flushed.
	  /// <p>
	  /// Disabled by default (writer flushes by RAM usage).
	  /// <p>
	  /// NOTE: this setting won't trigger a segment flush.
	  /// 
	  /// <p>
	  /// Takes effect immediately, but only the next time a document is added,
	  /// updated or deleted. Also, if you only delete-by-query, this setting has no
	  /// effect, i.e. delete queries are buffered until the next segment is flushed.
	  /// </summary>
	  /// <exception cref="IllegalArgumentException">
	  ///           if maxBufferedDeleteTerms is enabled but smaller than 1
	  /// </exception>
	  /// <seealso cref= #setRAMBufferSizeMB </seealso>
	  public virtual LiveIndexWriterConfig SetMaxBufferedDeleteTerms(int maxBufferedDeleteTerms)
	  {
		if (maxBufferedDeleteTerms != IndexWriterConfig.DISABLE_AUTO_FLUSH && maxBufferedDeleteTerms < 1)
		{
		  throw new System.ArgumentException("maxBufferedDeleteTerms must at least be 1 when enabled");
		}
		this.MaxBufferedDeleteTerms_Renamed = maxBufferedDeleteTerms;
		return this;
	  }

	  /// <summary>
	  /// Returns the number of buffered deleted terms that will trigger a flush of all
	  /// buffered deletes if enabled.
	  /// </summary>
	  /// <seealso cref= #setMaxBufferedDeleteTerms(int) </seealso>
	  public virtual int MaxBufferedDeleteTerms
	  {
		  get
		  {
			return MaxBufferedDeleteTerms_Renamed;
		  }
	  }

	  /// <summary>
	  /// Determines the amount of RAM that may be used for buffering added documents
	  /// and deletions before they are flushed to the Directory. Generally for
	  /// faster indexing performance it's best to flush by RAM usage instead of
	  /// document count and use as large a RAM buffer as you can.
	  /// <p>
	  /// When this is set, the writer will flush whenever buffered documents and
	  /// deletions use this much RAM. Pass in
	  /// <seealso cref="IndexWriterConfig#DISABLE_AUTO_FLUSH"/> to prevent triggering a flush
	  /// due to RAM usage. Note that if flushing by document count is also enabled,
	  /// then the flush will be triggered by whichever comes first.
	  /// <p>
	  /// The maximum RAM limit is inherently determined by the JVMs available
	  /// memory. Yet, an <seealso cref="IndexWriter"/> session can consume a significantly
	  /// larger amount of memory than the given RAM limit since this limit is just
	  /// an indicator when to flush memory resident documents to the Directory.
	  /// Flushes are likely happen concurrently while other threads adding documents
	  /// to the writer. For application stability the available memory in the JVM
	  /// should be significantly larger than the RAM buffer used for indexing.
	  /// <p>
	  /// <b>NOTE</b>: the account of RAM usage for pending deletions is only
	  /// approximate. Specifically, if you delete by Query, Lucene currently has no
	  /// way to measure the RAM usage of individual Queries so the accounting will
	  /// under-estimate and you should compensate by either calling commit()
	  /// periodically yourself, or by using <seealso cref="#setMaxBufferedDeleteTerms(int)"/>
	  /// to flush and apply buffered deletes by count instead of RAM usage (for each
	  /// buffered delete Query a constant number of bytes is used to estimate RAM
	  /// usage). Note that enabling <seealso cref="#setMaxBufferedDeleteTerms(int)"/> will not
	  /// trigger any segment flushes.
	  /// <p>
	  /// <b>NOTE</b>: It's not guaranteed that all memory resident documents are
	  /// flushed once this limit is exceeded. Depending on the configured
	  /// <seealso cref="FlushPolicy"/> only a subset of the buffered documents are flushed and
	  /// therefore only parts of the RAM buffer is released.
	  /// <p>
	  /// 
	  /// The default value is <seealso cref="IndexWriterConfig#DEFAULT_RAM_BUFFER_SIZE_MB"/>.
	  /// 
	  /// <p>
	  /// Takes effect immediately, but only the next time a document is added,
	  /// updated or deleted.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setRAMPerThreadHardLimitMB(int)
	  /// </seealso>
	  /// <exception cref="IllegalArgumentException">
	  ///           if ramBufferSize is enabled but non-positive, or it disables
	  ///           ramBufferSize when maxBufferedDocs is already disabled </exception>
	  public virtual LiveIndexWriterConfig SetRAMBufferSizeMB(double ramBufferSizeMB)
	  {
		if (ramBufferSizeMB != IndexWriterConfig.DISABLE_AUTO_FLUSH && ramBufferSizeMB <= 0.0)
		{
		  throw new System.ArgumentException("ramBufferSize should be > 0.0 MB when enabled");
		}
		if (ramBufferSizeMB == IndexWriterConfig.DISABLE_AUTO_FLUSH && MaxBufferedDocs_Renamed == IndexWriterConfig.DISABLE_AUTO_FLUSH)
		{
		  throw new System.ArgumentException("at least one of ramBufferSize and maxBufferedDocs must be enabled");
		}
		this.RamBufferSizeMB = ramBufferSizeMB;
		return this;
	  }

	  /// <summary>
	  /// Returns the value set by <seealso cref="#setRAMBufferSizeMB(double)"/> if enabled. </summary>
	  public virtual double RAMBufferSizeMB
	  {
		  get
		  {
			return RamBufferSizeMB;
		  }
	  }

	  /// <summary>
	  /// Determines the minimal number of documents required before the buffered
	  /// in-memory documents are flushed as a new Segment. Large values generally
	  /// give faster indexing.
	  /// 
	  /// <p>
	  /// When this is set, the writer will flush every maxBufferedDocs added
	  /// documents. Pass in <seealso cref="IndexWriterConfig#DISABLE_AUTO_FLUSH"/> to prevent
	  /// triggering a flush due to number of buffered documents. Note that if
	  /// flushing by RAM usage is also enabled, then the flush will be triggered by
	  /// whichever comes first.
	  /// 
	  /// <p>
	  /// Disabled by default (writer flushes by RAM usage).
	  /// 
	  /// <p>
	  /// Takes effect immediately, but only the next time a document is added,
	  /// updated or deleted.
	  /// </summary>
	  /// <seealso cref= #setRAMBufferSizeMB(double) </seealso>
	  /// <exception cref="IllegalArgumentException">
	  ///           if maxBufferedDocs is enabled but smaller than 2, or it disables
	  ///           maxBufferedDocs when ramBufferSize is already disabled </exception>
	  public virtual LiveIndexWriterConfig SetMaxBufferedDocs(int maxBufferedDocs)
	  {
		if (maxBufferedDocs != IndexWriterConfig.DISABLE_AUTO_FLUSH && maxBufferedDocs < 2)
		{
		  throw new System.ArgumentException("maxBufferedDocs must at least be 2 when enabled");
		}
		if (maxBufferedDocs == IndexWriterConfig.DISABLE_AUTO_FLUSH && RamBufferSizeMB == IndexWriterConfig.DISABLE_AUTO_FLUSH)
		{
		  throw new System.ArgumentException("at least one of ramBufferSize and maxBufferedDocs must be enabled");
		}
		this.MaxBufferedDocs_Renamed = maxBufferedDocs;
		return this;
	  }

	  /// <summary>
	  /// Returns the number of buffered added documents that will trigger a flush if
	  /// enabled.
	  /// </summary>
	  /// <seealso cref= #setMaxBufferedDocs(int) </seealso>
	  public virtual int MaxBufferedDocs
	  {
		  get
		  {
			return MaxBufferedDocs_Renamed;
		  }
	  }

	  /// <summary>
	  /// Set the merged segment warmer. See <seealso cref="IndexReaderWarmer"/>.
	  /// 
	  /// <p>
	  /// Takes effect on the next merge.
	  /// </summary>
	  public virtual LiveIndexWriterConfig SetMergedSegmentWarmer(IndexReaderWarmer mergeSegmentWarmer)
	  {
		this.MergedSegmentWarmer_Renamed = mergeSegmentWarmer;
		return this;
	  }

	  /// <summary>
	  /// Returns the current merged segment warmer. See <seealso cref="IndexReaderWarmer"/>. </summary>
	  public virtual IndexReaderWarmer MergedSegmentWarmer
	  {
		  get
		  {
			return MergedSegmentWarmer_Renamed;
		  }
	  }

	  /// <summary>
	  /// Sets the termsIndexDivisor passed to any readers that IndexWriter opens,
	  /// for example when applying deletes or creating a near-real-time reader in
	  /// <seealso cref="DirectoryReader#open(IndexWriter, boolean)"/>. If you pass -1, the
	  /// terms index won't be loaded by the readers. this is only useful in advanced
	  /// situations when you will only .next() through all terms; attempts to seek
	  /// will hit an exception.
	  /// 
	  /// <p>
	  /// Takes effect immediately, but only applies to readers opened after this
	  /// call
	  /// <p>
	  /// <b>NOTE:</b> divisor settings &gt; 1 do not apply to all PostingsFormat
	  /// implementations, including the default one in this release. It only makes
	  /// sense for terms indexes that can efficiently re-sample terms at load time.
	  /// </summary>
	  public virtual LiveIndexWriterConfig SetReaderTermsIndexDivisor(int divisor)
	  {
		if (divisor <= 0 && divisor != -1)
		{
		  throw new System.ArgumentException("divisor must be >= 1, or -1 (got " + divisor + ")");
		}
		ReaderTermsIndexDivisor_Renamed = divisor;
		return this;
	  }

	  /// <summary>
	  /// Returns the {@code termInfosIndexDivisor}.
	  /// </summary>
	  /// <seealso cref= #setReaderTermsIndexDivisor(int)  </seealso>
	  public virtual int ReaderTermsIndexDivisor
	  {
		  get
		  {
			return ReaderTermsIndexDivisor_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the <seealso cref="OpenMode"/> set by <seealso cref="IndexWriterConfig#setOpenMode(OpenMode)"/>. </summary>
	  public virtual OpenMode OpenMode
	  {
		  get
		  {
			return OpenMode_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the <seealso cref="IndexDeletionPolicy"/> specified in
	  /// <seealso cref="IndexWriterConfig#setIndexDeletionPolicy(IndexDeletionPolicy)"/> or
	  /// the default <seealso cref="KeepOnlyLastCommitDeletionPolicy"/>/
	  /// </summary>
	  public virtual IndexDeletionPolicy IndexDeletionPolicy
	  {
		  get
		  {
			return DelPolicy;
		  }
	  }

	  /// <summary>
	  /// Returns the <seealso cref="IndexCommit"/> as specified in
	  /// <seealso cref="IndexWriterConfig#setIndexCommit(IndexCommit)"/> or the default,
	  /// {@code null} which specifies to open the latest index commit point.
	  /// </summary>
	  public virtual IndexCommit IndexCommit
	  {
		  get
		  {
			return Commit;
		  }
	  }

	  /// <summary>
	  /// Expert: returns the <seealso cref="Similarity"/> implementation used by this
	  /// <seealso cref="IndexWriter"/>.
	  /// </summary>
	  public virtual Similarity Similarity
	  {
		  get
		  {
			return Similarity_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the <seealso cref="MergeScheduler"/> that was set by
	  /// <seealso cref="IndexWriterConfig#setMergeScheduler(MergeScheduler)"/>.
	  /// </summary>
	  public virtual MergeScheduler MergeScheduler
	  {
		  get
		  {
			return MergeScheduler_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns allowed timeout when acquiring the write lock.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setWriteLockTimeout(long) </seealso>
	  public virtual long WriteLockTimeout
	  {
		  get
		  {
			return WriteLockTimeout_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the current <seealso cref="Codec"/>. </summary>
	  public virtual Codec Codec
	  {
		  get
		  {
			return Codec_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the current MergePolicy in use by this writer.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setMergePolicy(MergePolicy) </seealso>
	  public virtual MergePolicy MergePolicy
	  {
		  get
		  {
			return MergePolicy_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the configured <seealso cref="DocumentsWriterPerThreadPool"/> instance.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setIndexerThreadPool(DocumentsWriterPerThreadPool) </seealso>
	  /// <returns> the configured <seealso cref="DocumentsWriterPerThreadPool"/> instance. </returns>
	  internal virtual DocumentsWriterPerThreadPool IndexerThreadPool
	  {
		  get
		  {
			return IndexerThreadPool_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the max number of simultaneous threads that may be indexing
	  /// documents at once in IndexWriter.
	  /// </summary>
	  public virtual int MaxThreadStates
	  {
		  get
		  {
			try
			{
			  return ((ThreadAffinityDocumentsWriterThreadPool) IndexerThreadPool_Renamed).MaxThreadStates;
			}
			catch (System.InvalidCastException cce)
			{
			  throw new IllegalStateException(cce);
			}
		  }
	  }

	  /// <summary>
	  /// Returns {@code true} if <seealso cref="IndexWriter"/> should pool readers even if
	  /// <seealso cref="DirectoryReader#open(IndexWriter, boolean)"/> has not been called.
	  /// </summary>
	  public virtual bool ReaderPooling
	  {
		  get
		  {
			return ReaderPooling_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the indexing chain set on
	  /// <seealso cref="IndexWriterConfig#setIndexingChain(IndexingChain)"/>.
	  /// </summary>
	  internal virtual IndexingChain IndexingChain
	  {
		  get
		  {
			return IndexingChain_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns the max amount of memory each <seealso cref="DocumentsWriterPerThread"/> can
	  /// consume until forcefully flushed.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setRAMPerThreadHardLimitMB(int) </seealso>
	  public virtual int RAMPerThreadHardLimitMB
	  {
		  get
		  {
			return PerThreadHardLimitMB;
		  }
	  }

	  /// <seealso cref= IndexWriterConfig#setFlushPolicy(FlushPolicy) </seealso>
	  internal virtual FlushPolicy FlushPolicy
	  {
		  get
		  {
			return FlushPolicy_Renamed;
		  }
	  }

	  /// <summary>
	  /// Returns <seealso cref="InfoStream"/> used for debugging.
	  /// </summary>
	  /// <seealso cref= IndexWriterConfig#setInfoStream(InfoStream) </seealso>
	  public virtual InfoStream InfoStream
	  {
		  get
		  {
			return InfoStream_Renamed;
		  }
	  }

	  /// <summary>
	  /// Sets if the <seealso cref="IndexWriter"/> should pack newly written segments in a
	  /// compound file. Default is <code>true</code>.
	  /// <p>
	  /// Use <code>false</code> for batch indexing with very large ram buffer
	  /// settings.
	  /// </p>
	  /// <p>
	  /// <b>Note: To control compound file usage during segment merges see
	  /// <seealso cref="MergePolicy#setNoCFSRatio(double)"/> and
	  /// <seealso cref="MergePolicy#setMaxCFSSegmentSizeMB(double)"/>. this setting only
	  /// applies to newly created segments.</b>
	  /// </p>
	  /// </summary>
	  public virtual LiveIndexWriterConfig SetUseCompoundFile(bool useCompoundFile)
	  {
		this.UseCompoundFile_Renamed = useCompoundFile;
		return this;
	  }

	  /// <summary>
	  /// Returns <code>true</code> iff the <seealso cref="IndexWriter"/> packs
	  /// newly written segments in a compound file. Default is <code>true</code>.
	  /// </summary>
	  public virtual bool UseCompoundFile
	  {
		  get
		  {
			return UseCompoundFile_Renamed;
		  }
	  }

	  /// <summary>
	  /// Sets if <seealso cref="IndexWriter"/> should call <seealso cref="AtomicReader#checkIntegrity()"/>
	  /// on existing segments before merging them into a new one.
	  /// <p>
	  /// Use <code>true</code> to enable this safety check, which can help
	  /// reduce the risk of propagating index corruption from older segments 
	  /// into new ones, at the expense of slower merging.
	  /// </p>
	  /// </summary>
	  public virtual LiveIndexWriterConfig SetCheckIntegrityAtMerge(bool checkIntegrityAtMerge)
	  {
		this.CheckIntegrityAtMerge_Renamed = checkIntegrityAtMerge;
		return this;
	  }

	  /// <summary>
	  /// Returns true if <seealso cref="AtomicReader#checkIntegrity()"/> is called before 
	  ///  merging segments. 
	  /// </summary>
	  public virtual bool CheckIntegrityAtMerge
	  {
		  get
		  {
			return CheckIntegrityAtMerge_Renamed;
		  }
	  }

	  public override string ToString()
	  {
		StringBuilder sb = new StringBuilder();
		sb.Append("matchVersion=").Append(MatchVersion).Append("\n");
		sb.Append("analyzer=").Append(Analyzer_Renamed == null ? "null" : Analyzer_Renamed.GetType().Name).Append("\n");
		sb.Append("ramBufferSizeMB=").Append(RAMBufferSizeMB).Append("\n");
		sb.Append("maxBufferedDocs=").Append(MaxBufferedDocs).Append("\n");
		sb.Append("maxBufferedDeleteTerms=").Append(MaxBufferedDeleteTerms).Append("\n");
		sb.Append("mergedSegmentWarmer=").Append(MergedSegmentWarmer).Append("\n");
		sb.Append("readerTermsIndexDivisor=").Append(ReaderTermsIndexDivisor).Append("\n");
		sb.Append("termIndexInterval=").Append(TermIndexInterval).Append("\n"); // TODO: this should be private to the codec, not settable here
		sb.Append("delPolicy=").Append(IndexDeletionPolicy.GetType().Name).Append("\n");
		IndexCommit commit = IndexCommit;
		sb.Append("commit=").Append(commit == null ? "null" : commit).Append("\n");
		sb.Append("openMode=").Append(OpenMode).Append("\n");
		sb.Append("similarity=").Append(Similarity.GetType().Name).Append("\n");
		sb.Append("mergeScheduler=").Append(MergeScheduler).Append("\n");
		sb.Append("default WRITE_LOCK_TIMEOUT=").Append(IndexWriterConfig.WRITE_LOCK_TIMEOUT).Append("\n");
		sb.Append("writeLockTimeout=").Append(WriteLockTimeout).Append("\n");
		sb.Append("codec=").Append(Codec).Append("\n");
		sb.Append("infoStream=").Append(InfoStream.GetType().Name).Append("\n");
		sb.Append("mergePolicy=").Append(MergePolicy).Append("\n");
		sb.Append("indexerThreadPool=").Append(IndexerThreadPool).Append("\n");
		sb.Append("readerPooling=").Append(ReaderPooling).Append("\n");
		sb.Append("perThreadHardLimitMB=").Append(RAMPerThreadHardLimitMB).Append("\n");
		sb.Append("useCompoundFile=").Append(UseCompoundFile).Append("\n");
		sb.Append("checkIntegrityAtMerge=").Append(CheckIntegrityAtMerge).Append("\n");
		return sb.ToString();
	  }



	}

}