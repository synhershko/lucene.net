namespace Lucene.Net.Index
{

	/*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements. See the NOTICE file distributed with
	 * this work for additional information regarding copyright ownership.
	 * The ASF licenses this file to You under the Apache License, Version 2.0
	 * (the "License"); you may not use this file except in compliance with
	 * the License. You may obtain a copy of the License at
	 *
	 * http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS,
	 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	 * See the License for the specific language governing permissions and
	 * limitations under the License.
	 */

	using ThreadState = Lucene.Net.Index.DocumentsWriterPerThreadPool.ThreadState;

	/// <summary>
	/// Default <seealso cref="FlushPolicy"/> implementation that flushes new segments based on
	/// RAM used and document count depending on the IndexWriter's
	/// <seealso cref="IndexWriterConfig"/>. It also applies pending deletes based on the
	/// number of buffered delete terms.
	/// 
	/// <ul>
	/// <li>
	/// <seealso cref="#onDelete(DocumentsWriterFlushControl, DocumentsWriterPerThreadPool.ThreadState)"/>
	/// - applies pending delete operations based on the global number of buffered
	/// delete terms iff <seealso cref="IndexWriterConfig#getMaxBufferedDeleteTerms()"/> is
	/// enabled</li>
	/// <li>
	/// <seealso cref="#onInsert(DocumentsWriterFlushControl, DocumentsWriterPerThreadPool.ThreadState)"/>
	/// - flushes either on the number of documents per
	/// <seealso cref="DocumentsWriterPerThread"/> (
	/// <seealso cref="DocumentsWriterPerThread#getNumDocsInRAM()"/>) or on the global active
	/// memory consumption in the current indexing session iff
	/// <seealso cref="IndexWriterConfig#getMaxBufferedDocs()"/> or
	/// <seealso cref="IndexWriterConfig#getRAMBufferSizeMB()"/> is enabled respectively</li>
	/// <li>
	/// <seealso cref="#onUpdate(DocumentsWriterFlushControl, DocumentsWriterPerThreadPool.ThreadState)"/>
	/// - calls
	/// <seealso cref="#onInsert(DocumentsWriterFlushControl, DocumentsWriterPerThreadPool.ThreadState)"/>
	/// and
	/// <seealso cref="#onDelete(DocumentsWriterFlushControl, DocumentsWriterPerThreadPool.ThreadState)"/>
	/// in order</li>
	/// </ul>
	/// All <seealso cref="IndexWriterConfig"/> settings are used to mark
	/// <seealso cref="DocumentsWriterPerThread"/> as flush pending during indexing with
	/// respect to their live updates.
	/// <p>
	/// If <seealso cref="IndexWriterConfig#setRAMBufferSizeMB(double)"/> is enabled, the
	/// largest ram consuming <seealso cref="DocumentsWriterPerThread"/> will be marked as
	/// pending iff the global active RAM consumption is >= the configured max RAM
	/// buffer.
	/// </summary>
	internal class FlushByRamOrCountsPolicy : FlushPolicy
	{

	  public override void OnDelete(DocumentsWriterFlushControl control, ThreadState state)
	  {
		if (FlushOnDeleteTerms())
		{
		  // Flush this state by num del terms
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int maxBufferedDeleteTerms = indexWriterConfig.getMaxBufferedDeleteTerms();
		  int maxBufferedDeleteTerms = IndexWriterConfig.MaxBufferedDeleteTerms;
		  if (control.NumGlobalTermDeletes >= maxBufferedDeleteTerms)
		  {
			control.SetApplyAllDeletes();
		  }
		}
		if ((FlushOnRAM() && control.DeleteBytesUsed > (1024 * 1024 * IndexWriterConfig.RAMBufferSizeMB)))
		{
		  control.SetApplyAllDeletes();
		 if (InfoStream.IsEnabled("FP"))
		 {
		   InfoStream.Message("FP", "force apply deletes bytesUsed=" + control.DeleteBytesUsed + " vs ramBuffer=" + (1024 * 1024 * IndexWriterConfig.RAMBufferSizeMB));
		 }
		}
	  }

	  public override void OnInsert(DocumentsWriterFlushControl control, ThreadState state)
	  {
		if (FlushOnDocCount() && state.Dwpt.NumDocsInRAM >= IndexWriterConfig.MaxBufferedDocs)
		{
		  // Flush this state by num docs
		  control.FlushPending = state;
		} // flush by RAM
		else if (FlushOnRAM())
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final long limit = (long)(indexWriterConfig.getRAMBufferSizeMB() * 1024.d * 1024.d);
		  long limit = (long)(IndexWriterConfig.RAMBufferSizeMB * 1024.d * 1024.d);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final long totalRam = control.activeBytes() + control.getDeleteBytesUsed();
		  long totalRam = control.ActiveBytes() + control.DeleteBytesUsed;
		  if (totalRam >= limit)
		  {
			if (InfoStream.IsEnabled("FP"))
			{
			  InfoStream.Message("FP", "flush: activeBytes=" + control.ActiveBytes() + " deleteBytes=" + control.DeleteBytesUsed + " vs limit=" + limit);
			}
			MarkLargestWriterPending(control, state, totalRam);
		  }
		}
	  }

	  /// <summary>
	  /// Marks the most ram consuming active <seealso cref="DocumentsWriterPerThread"/> flush
	  /// pending
	  /// </summary>
	  protected internal virtual void MarkLargestWriterPending(DocumentsWriterFlushControl control, ThreadState perThreadState, long currentBytesPerThread)
	  {
		control.FlushPending = FindLargestNonPendingWriter(control, perThreadState);
	  }

	  /// <summary>
	  /// Returns <code>true</code> if this <seealso cref="FlushPolicy"/> flushes on
	  /// <seealso cref="IndexWriterConfig#getMaxBufferedDocs()"/>, otherwise
	  /// <code>false</code>.
	  /// </summary>
	  protected internal virtual bool FlushOnDocCount()
	  {
		return IndexWriterConfig.MaxBufferedDocs != IndexWriterConfig.DISABLE_AUTO_FLUSH;
	  }

	  /// <summary>
	  /// Returns <code>true</code> if this <seealso cref="FlushPolicy"/> flushes on
	  /// <seealso cref="IndexWriterConfig#getMaxBufferedDeleteTerms()"/>, otherwise
	  /// <code>false</code>.
	  /// </summary>
	  protected internal virtual bool FlushOnDeleteTerms()
	  {
		return IndexWriterConfig.MaxBufferedDeleteTerms != IndexWriterConfig.DISABLE_AUTO_FLUSH;
	  }

	  /// <summary>
	  /// Returns <code>true</code> if this <seealso cref="FlushPolicy"/> flushes on
	  /// <seealso cref="IndexWriterConfig#getRAMBufferSizeMB()"/>, otherwise
	  /// <code>false</code>.
	  /// </summary>
	  protected internal virtual bool FlushOnRAM()
	  {
		return IndexWriterConfig.RAMBufferSizeMB != IndexWriterConfig.DISABLE_AUTO_FLUSH;
	  }
	}

}