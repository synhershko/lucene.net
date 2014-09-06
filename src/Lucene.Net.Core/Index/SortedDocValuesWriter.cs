using Lucene.Net.Codecs;
using Lucene.Net.Util;
using Lucene.Net.Util.Packed;
using System.Collections.Generic;
using System.Diagnostics;

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

    /// <summary>
    /// Buffers up pending byte[] per doc, deref and sorting via
    ///  int ord, then flushes when segment flushes.
    /// </summary>
    internal class SortedDocValuesWriter : DocValuesWriter
    {
        internal readonly BytesRefHash Hash;
        private AppendingDeltaPackedLongBuffer Pending;
        private readonly Counter IwBytesUsed;
        private long BytesUsed; // this currently only tracks differences in 'pending'
        private readonly FieldInfo FieldInfo;

        private const int EMPTY_ORD = -1;

        public SortedDocValuesWriter(FieldInfo fieldInfo, Counter iwBytesUsed)
        {
            this.FieldInfo = fieldInfo;
            this.IwBytesUsed = iwBytesUsed;
            Hash = new BytesRefHash(new ByteBlockPool(new ByteBlockPool.DirectTrackingAllocator(iwBytesUsed)), BytesRefHash.DEFAULT_CAPACITY, new BytesRefHash.DirectBytesStartArray(BytesRefHash.DEFAULT_CAPACITY, iwBytesUsed));
            Pending = new AppendingDeltaPackedLongBuffer(PackedInts.COMPACT);
            BytesUsed = Pending.RamBytesUsed();
            iwBytesUsed.AddAndGet(BytesUsed);
        }

        public virtual void AddValue(int docID, BytesRef value)
        {
            if (docID < Pending.Size())
            {
                throw new System.ArgumentException("DocValuesField \"" + FieldInfo.Name + "\" appears more than once in this document (only one value is allowed per field)");
            }
            if (value == null)
            {
                throw new System.ArgumentException("field \"" + FieldInfo.Name + "\": null value not allowed");
            }
            if (value.Length > (ByteBlockPool.BYTE_BLOCK_SIZE - 2))
            {
                throw new System.ArgumentException("DocValuesField \"" + FieldInfo.Name + "\" is too large, must be <= " + (ByteBlockPool.BYTE_BLOCK_SIZE - 2));
            }

            // Fill in any holes:
            while (Pending.Size() < docID)
            {
                Pending.Add(EMPTY_ORD);
            }

            AddOneValue(value);
        }

        internal override void Finish(int maxDoc)
        {
            while (Pending.Size() < maxDoc)
            {
                Pending.Add(EMPTY_ORD);
            }
            UpdateBytesUsed();
        }

        private void AddOneValue(BytesRef value)
        {
            int termID = Hash.Add(value);
            if (termID < 0)
            {
                termID = -termID - 1;
            }
            else
            {
                // reserve additional space for each unique value:
                // 1. when indexing, when hash is 50% full, rehash() suddenly needs 2*size ints.
                //    TODO: can this same OOM happen in THPF?
                // 2. when flushing, we need 1 int per value (slot in the ordMap).
                IwBytesUsed.AddAndGet(2 * RamUsageEstimator.NUM_BYTES_INT);
            }

            Pending.Add(termID);
            UpdateBytesUsed();
        }

        private void UpdateBytesUsed()
        {
            long newBytesUsed = Pending.RamBytesUsed();
            IwBytesUsed.AddAndGet(newBytesUsed - BytesUsed);
            BytesUsed = newBytesUsed;
        }

        internal override void Flush(SegmentWriteState state, DocValuesConsumer dvConsumer)
        {
            int maxDoc = state.SegmentInfo.DocCount;

            Debug.Assert(Pending.Size() == maxDoc);
            int valueCount = Hash.Size();

            int[] sortedValues = Hash.Sort(BytesRef.UTF8SortedAsUnicodeComparer);
            int[] ordMap = new int[valueCount];

            for (int ord = 0; ord < valueCount; ord++)
            {
                ordMap[sortedValues[ord]] = ord;
            }

            dvConsumer.AddSortedField(FieldInfo, GetBytesRefEnumberable(valueCount, sortedValues),
                // doc -> ord
                                      GetOrdsEnumberable(maxDoc, ordMap));
        }

        internal override void Abort()
        {
        }

        private IEnumerable<BytesRef> GetBytesRefEnumberable(int valueCount, int[] sortedValues)
        {
            BytesRef scratch = new BytesRef();

            for (int i = 0; i < valueCount; ++i)
            {
                yield return Hash.Get(sortedValues[i], scratch);
            }
        }

        private IEnumerable<long> GetOrdsEnumberable(int maxDoc, int[] ordMap)
        {
            AppendingDeltaPackedLongBuffer.Iterator iter = Pending.GetIterator();

            for (int i = 0; i < maxDoc; ++i)
            {
                int ord = (int)iter.Next();
                yield return ord == -1 ? ord : ordMap[ord];
            }
        }

        /*
	  private class IterableAnonymousInnerClassHelper : IEnumerable<BytesRef>
	  {
		  private readonly SortedDocValuesWriter OuterInstance;

		  private int ValueCount;
		  private int[] SortedValues;

		  public IterableAnonymousInnerClassHelper(SortedDocValuesWriter outerInstance, int valueCount, int[] sortedValues)
		  {
			  this.OuterInstance = outerInstance;
			  this.ValueCount = valueCount;
			  this.SortedValues = sortedValues;
		  }

									// ord -> value
		  public virtual IEnumerator<BytesRef> GetEnumerator()
		  {
			return new ValuesIterator(OuterInstance, SortedValues, ValueCount);
		  }
	  }

	  private class IterableAnonymousInnerClassHelper2 : IEnumerable<Number>
	  {
		  private readonly SortedDocValuesWriter OuterInstance;

		  private int MaxDoc;
		  private int[] OrdMap;

		  public IterableAnonymousInnerClassHelper2(SortedDocValuesWriter outerInstance, int maxDoc, int[] ordMap)
		  {
			  this.OuterInstance = outerInstance;
			  this.MaxDoc = maxDoc;
			  this.OrdMap = ordMap;
		  }

		  public virtual IEnumerator<Number> GetEnumerator()
		  {
			return new OrdsIterator(OuterInstance, OrdMap, MaxDoc);
		  }
	  }

	  public override void Abort()
	  {
	  }

	  // iterates over the unique values we have in ram
	  private class ValuesIterator : IEnumerator<BytesRef>
	  {
		  private readonly SortedDocValuesWriter OuterInstance;

		internal readonly int[] SortedValues;
		internal readonly BytesRef Scratch = new BytesRef();
		internal readonly int ValueCount;
		internal int OrdUpto;

		internal ValuesIterator(SortedDocValuesWriter outerInstance, int[] sortedValues, int valueCount)
		{
			this.OuterInstance = outerInstance;
		  this.SortedValues = sortedValues;
		  this.ValueCount = valueCount;
		}

		public override bool HasNext()
		{
		  return OrdUpto < ValueCount;
		}

		public override BytesRef Next()
		{
		  if (!HasNext())
		  {
			throw new Exception();
		  }
		  OuterInstance.Hash.Get(SortedValues[OrdUpto], Scratch);
		  OrdUpto++;
		  return Scratch;
		}

		public override void Remove()
		{
		  throw new System.NotSupportedException();
		}
	  }

	  // iterates over the ords for each doc we have in ram
	  private class OrdsIterator : IEnumerator<Number>
	  {
		  internal bool InstanceFieldsInitialized = false;

		  internal virtual void InitializeInstanceFields()
		  {
			  Iter = OuterInstance.Pending.Iterator();
		  }

		  private readonly SortedDocValuesWriter OuterInstance;

		internal AppendingDeltaPackedLongBuffer.Iterator Iter;
		internal readonly int[] OrdMap;
		internal readonly int MaxDoc;
		internal int DocUpto;

		internal OrdsIterator(SortedDocValuesWriter outerInstance, int[] ordMap, int maxDoc)
		{
			this.OuterInstance = outerInstance;

			if (!InstanceFieldsInitialized)
			{
				InitializeInstanceFields();
				InstanceFieldsInitialized = true;
			}
		  this.OrdMap = ordMap;
		  this.MaxDoc = maxDoc;
		  Debug.Assert(outerInstance.Pending.Size() == maxDoc);
		}

		public override bool HasNext()
		{
		  return DocUpto < MaxDoc;
		}

		public override Number Next()
		{
		  if (!HasNext())
		  {
			throw new Exception();
		  }
		  int ord = (int) Iter.next();
		  DocUpto++;
		  return ord == -1 ? ord : OrdMap[ord];
		}

		public override void Remove()
		{
		  throw new System.NotSupportedException();
		}
	  }*/
    }
}