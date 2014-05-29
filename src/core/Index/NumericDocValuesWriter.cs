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


	using DocValuesConsumer = Lucene.Net.Codecs.DocValuesConsumer;
	using Counter = Lucene.Net.Util.Counter;
	using FixedBitSet = Lucene.Net.Util.FixedBitSet;
	using RamUsageEstimator = Lucene.Net.Util.RamUsageEstimator;
	using AppendingDeltaPackedLongBuffer = Lucene.Net.Util.Packed.AppendingDeltaPackedLongBuffer;
	using PackedInts = Lucene.Net.Util.Packed.PackedInts;

	/// <summary>
	/// Buffers up pending long per doc, then flushes when
	///  segment flushes. 
	/// </summary>
	internal class NumericDocValuesWriter : DocValuesWriter
	{

	  private const long MISSING = 0L;

	  private AppendingDeltaPackedLongBuffer Pending;
	  private readonly Counter IwBytesUsed;
	  private long BytesUsed;
	  private FixedBitSet DocsWithField;
	  private readonly FieldInfo FieldInfo;

	  public NumericDocValuesWriter(FieldInfo fieldInfo, Counter iwBytesUsed, bool trackDocsWithField)
	  {
		Pending = new AppendingDeltaPackedLongBuffer(PackedInts.COMPACT);
		DocsWithField = trackDocsWithField ? new FixedBitSet(64) : null;
		BytesUsed = Pending.RamBytesUsed() + DocsWithFieldBytesUsed();
		this.FieldInfo = fieldInfo;
		this.IwBytesUsed = iwBytesUsed;
		iwBytesUsed.AddAndGet(BytesUsed);
	  }

	  public virtual void AddValue(int docID, long value)
	  {
		if (docID < Pending.Size())
		{
		  throw new System.ArgumentException("DocValuesField \"" + FieldInfo.Name + "\" appears more than once in this document (only one value is allowed per field)");
		}

		// Fill in any holes:
		for (int i = (int)Pending.Size(); i < docID; ++i)
		{
		  Pending.Add(MISSING);
		}

		Pending.Add(value);
		if (DocsWithField != null)
		{
		  DocsWithField = FixedBitSet.EnsureCapacity(DocsWithField, docID);
		  DocsWithField.Set(docID);
		}

		UpdateBytesUsed();
	  }

	  private long DocsWithFieldBytesUsed()
	  {
		// size of the long[] + some overhead
		return DocsWithField == null ? 0 : RamUsageEstimator.SizeOf(DocsWithField.Bits) + 64;
	  }

	  private void UpdateBytesUsed()
	  {
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final long newBytesUsed = pending.ramBytesUsed() + docsWithFieldBytesUsed();
		long newBytesUsed = Pending.RamBytesUsed() + DocsWithFieldBytesUsed();
		IwBytesUsed.AddAndGet(newBytesUsed - BytesUsed);
		BytesUsed = newBytesUsed;
	  }

	  public override void Finish(int maxDoc)
	  {
	  }

	  public override void Flush(SegmentWriteState state, DocValuesConsumer dvConsumer)
	  {

//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int maxDoc = state.segmentInfo.getDocCount();
		int maxDoc = state.SegmentInfo.DocCount;

		dvConsumer.AddNumericField(FieldInfo, new IterableAnonymousInnerClassHelper(this, maxDoc));
	  }

	  private class IterableAnonymousInnerClassHelper : IEnumerable<Number>
	  {
		  private readonly NumericDocValuesWriter OuterInstance;

		  private int MaxDoc;

		  public IterableAnonymousInnerClassHelper(NumericDocValuesWriter outerInstance, int maxDoc)
		  {
			  this.OuterInstance = outerInstance;
			  this.MaxDoc = maxDoc;
		  }

		  public virtual IEnumerator<Number> GetEnumerator()
		  {
			return new NumericIterator(OuterInstance, MaxDoc);
		  }
	  }

	  public override void Abort()
	  {
	  }

	  // iterates over the values we have in ram
	  private class NumericIterator : IEnumerator<Number>
	  {
		  internal bool InstanceFieldsInitialized = false;

		  internal virtual void InitializeInstanceFields()
		  {
			  Iter = outerInstance.Pending.Iterator();
			  Size = (int)outerInstance.Pending.Size();
		  }

		  private readonly NumericDocValuesWriter OuterInstance;

		internal AppendingDeltaPackedLongBuffer.Iterator Iter;
		internal int Size;
		internal readonly int MaxDoc;
		internal int Upto;

		internal NumericIterator(NumericDocValuesWriter outerInstance, int maxDoc)
		{
			this.OuterInstance = outerInstance;

			if (!InstanceFieldsInitialized)
			{
				InitializeInstanceFields();
				InstanceFieldsInitialized = true;
			}
		  this.MaxDoc = maxDoc;
		}

		public override bool HasNext()
		{
		  return Upto < MaxDoc;
		}

		public override Number Next()
		{
		  if (!HasNext())
		  {
			throw new NoSuchElementException();
		  }
		  long? value;
		  if (Upto < Size)
		  {
			long v = Iter.next();
			if (outerInstance.DocsWithField == null || outerInstance.DocsWithField.Get(Upto))
			{
			  value = v;
			}
			else
			{
			  value = null;
			}
		  }
		  else
		  {
			value = outerInstance.DocsWithField != null ? null : MISSING;
		  }
		  Upto++;
		  return value;
		}

		public override void Remove()
		{
		  throw new System.NotSupportedException();
		}
	  }
	}

}