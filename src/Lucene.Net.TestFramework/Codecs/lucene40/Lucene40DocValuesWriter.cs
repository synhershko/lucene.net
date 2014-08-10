using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lucene.Net.Codecs.Lucene40
{
    using BytesRef = Lucene.Net.Util.BytesRef;
    using CompoundFileDirectory = Lucene.Net.Store.CompoundFileDirectory;
    using Directory = Lucene.Net.Store.Directory;
    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using IndexFileNames = Lucene.Net.Index.IndexFileNames;
    using IndexOutput = Lucene.Net.Store.IndexOutput;
    using IOUtils = Lucene.Net.Util.IOUtils;

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

    using LegacyDocValuesType = Lucene.Net.Codecs.Lucene40.Lucene40FieldInfosReader.LegacyDocValuesType;
    using PackedInts = Lucene.Net.Util.Packed.PackedInts;
    using SegmentWriteState = Lucene.Net.Index.SegmentWriteState;

    internal class Lucene40DocValuesWriter : DocValuesConsumer
    {
        private readonly Directory Dir;
        private readonly SegmentWriteState State;
        private readonly string LegacyKey;
        private const string SegmentSuffix = "dv";

        // note: intentionally ignores seg suffix
        internal Lucene40DocValuesWriter(SegmentWriteState state, string filename, string legacyKey)
        {
            this.State = state;
            this.LegacyKey = legacyKey;
            this.Dir = new CompoundFileDirectory(state.Directory, filename, state.Context, true);
        }

        public override void AddNumericField(FieldInfo field, IEnumerable<long> values)
        {
            // examine the values to determine best type to use
            long minValue = long.MaxValue;
            long maxValue = long.MinValue;
            foreach (long n in values)
            {
                long v = n == null ? 0 : (long)n;
                minValue = Math.Min(minValue, v);
                maxValue = Math.Max(maxValue, v);
            }

            string fileName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "dat");
            IndexOutput data = Dir.CreateOutput(fileName, State.Context);
            bool success = false;
            try
            {
                if (minValue >= sbyte.MinValue && maxValue <= sbyte.MaxValue && PackedInts.BitsRequired(maxValue - minValue) > 4)
                {
                    // fits in a byte[], would be more than 4bpv, just write byte[]
                    AddBytesField(field, data, values);
                }
                else if (minValue >= short.MinValue && maxValue <= short.MaxValue && PackedInts.BitsRequired(maxValue - minValue) > 8)
                {
                    // fits in a short[], would be more than 8bpv, just write short[]
                    AddShortsField(field, data, values);
                }
                else if (minValue >= int.MinValue && maxValue <= int.MaxValue && PackedInts.BitsRequired(maxValue - minValue) > 16)
                {
                    // fits in a int[], would be more than 16bpv, just write int[]
                    AddIntsField(field, data, values);
                }
                else
                {
                    AddVarIntsField(field, data, values, minValue, maxValue);
                }
                success = true;
            }
            finally
            {
                if (success)
                {
                    IOUtils.Close(data);
                }
                else
                {
                    IOUtils.CloseWhileHandlingException(data);
                }
            }
        }

        private void AddBytesField(FieldInfo field, IndexOutput output, IEnumerable<long> values)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.FIXED_INTS_8.Name);
            CodecUtil.WriteHeader(output, Lucene40DocValuesFormat.INTS_CODEC_NAME, Lucene40DocValuesFormat.INTS_VERSION_CURRENT);
            output.WriteInt(1); // size
            foreach (long n in values)
            {
                output.WriteByte(n == null ? (byte)0 : (byte)n);
            }
        }

        private void AddShortsField(FieldInfo field, IndexOutput output, IEnumerable<long> values)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.FIXED_INTS_16.Name);
            CodecUtil.WriteHeader(output, Lucene40DocValuesFormat.INTS_CODEC_NAME, Lucene40DocValuesFormat.INTS_VERSION_CURRENT);
            output.WriteInt(2); // size
            foreach (long n in values)
            {
                output.WriteShort(n == null ? (short)0 : (short)n);
            }
        }

        private void AddIntsField(FieldInfo field, IndexOutput output, IEnumerable<long> values)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.FIXED_INTS_32.Name);
            CodecUtil.WriteHeader(output, Lucene40DocValuesFormat.INTS_CODEC_NAME, Lucene40DocValuesFormat.INTS_VERSION_CURRENT);
            output.WriteInt(4); // size
            foreach (long n in values)
            {
                output.WriteInt(n == null ? 0 : (int)n);
            }
        }

        private void AddVarIntsField(FieldInfo field, IndexOutput output, IEnumerable<long> values, long minValue, long maxValue)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.VAR_INTS.Name);

            CodecUtil.WriteHeader(output, Lucene40DocValuesFormat.VAR_INTS_CODEC_NAME, Lucene40DocValuesFormat.VAR_INTS_VERSION_CURRENT);

            long delta = maxValue - minValue;

            if (delta < 0)
            {
                // writes longs
                output.WriteByte(Lucene40DocValuesFormat.VAR_INTS_FIXED_64);
                foreach (long n in values)
                {
                    output.WriteLong(n == null ? 0 : n);
                }
            }
            else
            {
                // writes packed ints
                output.WriteByte(Lucene40DocValuesFormat.VAR_INTS_PACKED);
                output.WriteLong(minValue);
                output.WriteLong(0 - minValue); // default value (representation of 0)
                PackedInts.Writer writer = PackedInts.GetWriter(output, State.SegmentInfo.DocCount, PackedInts.BitsRequired(delta), PackedInts.DEFAULT);
                foreach (long n in values)
                {
                    long v = n == null ? 0 : (long)n;
                    writer.Add(v - minValue);
                }
                writer.Finish();
            }
        }

        public override void AddBinaryField(FieldInfo field, IEnumerable<BytesRef> values)
        {
            // examine the values to determine best type to use
            HashSet<BytesRef> uniqueValues = new HashSet<BytesRef>();
            int minLength = int.MaxValue;
            int maxLength = int.MinValue;
            BytesRef brefDummy;
            foreach (BytesRef b in values)
            {
                brefDummy = b;

                if (b == null)
                {
                    brefDummy = new BytesRef(); // 4.0 doesnt distinguish
                }
                if (b.Length > Lucene40DocValuesFormat.MAX_BINARY_FIELD_LENGTH)
                {
                    throw new System.ArgumentException("DocValuesField \"" + field.Name + "\" is too large, must be <= " + Lucene40DocValuesFormat.MAX_BINARY_FIELD_LENGTH);
                }
                minLength = Math.Min(minLength, brefDummy.Length);
                maxLength = Math.Max(maxLength, brefDummy.Length);
                if (uniqueValues != null)
                {
                    if (uniqueValues.Add(BytesRef.DeepCopyOf(brefDummy)))
                    {
                        if (uniqueValues.Count > 256)
                        {
                            uniqueValues = null;
                        }
                    }
                }
            }

            int maxDoc = State.SegmentInfo.DocCount;
            bool @fixed = minLength == maxLength;
            bool dedup = uniqueValues != null && uniqueValues.Count * 2 < maxDoc;

            if (dedup)
            {
                // we will deduplicate and deref values
                bool success = false;
                IndexOutput data = null;
                IndexOutput index = null;
                string dataName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "dat");
                string indexName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "idx");
                try
                {
                    data = Dir.CreateOutput(dataName, State.Context);
                    index = Dir.CreateOutput(indexName, State.Context);
                    if (@fixed)
                    {
                        AddFixedDerefBytesField(field, data, index, values, minLength);
                    }
                    else
                    {
                        AddVarDerefBytesField(field, data, index, values);
                    }
                    success = true;
                }
                finally
                {
                    if (success)
                    {
                        IOUtils.Close(data, index);
                    }
                    else
                    {
                        IOUtils.CloseWhileHandlingException(data, index);
                    }
                }
            }
            else
            {
                // we dont deduplicate, just write values straight
                if (@fixed)
                {
                    // fixed byte[]
                    string fileName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "dat");
                    IndexOutput data = Dir.CreateOutput(fileName, State.Context);
                    bool success = false;
                    try
                    {
                        AddFixedStraightBytesField(field, data, values, minLength);
                        success = true;
                    }
                    finally
                    {
                        if (success)
                        {
                            IOUtils.Close(data);
                        }
                        else
                        {
                            IOUtils.CloseWhileHandlingException(data);
                        }
                    }
                }
                else
                {
                    // variable byte[]
                    bool success = false;
                    IndexOutput data = null;
                    IndexOutput index = null;
                    string dataName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "dat");
                    string indexName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "idx");
                    try
                    {
                        data = Dir.CreateOutput(dataName, State.Context);
                        index = Dir.CreateOutput(indexName, State.Context);
                        AddVarStraightBytesField(field, data, index, values);
                        success = true;
                    }
                    finally
                    {
                        if (success)
                        {
                            IOUtils.Close(data, index);
                        }
                        else
                        {
                            IOUtils.CloseWhileHandlingException(data, index);
                        }
                    }
                }
            }
        }

        private void AddFixedStraightBytesField(FieldInfo field, IndexOutput output, IEnumerable<BytesRef> values, int length)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_FIXED_STRAIGHT.Name);

            CodecUtil.WriteHeader(output, Lucene40DocValuesFormat.BYTES_FIXED_STRAIGHT_CODEC_NAME, Lucene40DocValuesFormat.BYTES_FIXED_STRAIGHT_VERSION_CURRENT);

            output.WriteInt(length);
            foreach (BytesRef v in values)
            {
                if (v != null)
                {
                    output.WriteBytes(v.Bytes, v.Offset, v.Length);
                }
            }
        }

        // NOTE: 4.0 file format docs are crazy/wrong here...
        private void AddVarStraightBytesField(FieldInfo field, IndexOutput data, IndexOutput index, IEnumerable<BytesRef> values)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_VAR_STRAIGHT.Name);

            CodecUtil.WriteHeader(data, Lucene40DocValuesFormat.BYTES_VAR_STRAIGHT_CODEC_NAME_DAT, Lucene40DocValuesFormat.BYTES_VAR_STRAIGHT_VERSION_CURRENT);

            CodecUtil.WriteHeader(index, Lucene40DocValuesFormat.BYTES_VAR_STRAIGHT_CODEC_NAME_IDX, Lucene40DocValuesFormat.BYTES_VAR_STRAIGHT_VERSION_CURRENT);

            /* values */

            long startPos = data.FilePointer;

            foreach (BytesRef v in values)
            {
                if (v != null)
                {
                    data.WriteBytes(v.Bytes, v.Offset, v.Length);
                }
            }

            /* addresses */

            long maxAddress = data.FilePointer - startPos;
            index.WriteVLong(maxAddress);

            int maxDoc = State.SegmentInfo.DocCount;
            Debug.Assert(maxDoc != int.MaxValue); // unsupported by the 4.0 impl

            PackedInts.Writer w = PackedInts.GetWriter(index, maxDoc + 1, PackedInts.BitsRequired(maxAddress), PackedInts.DEFAULT);
            long currentPosition = 0;
            foreach (BytesRef v in values)
            {
                w.Add(currentPosition);
                if (v != null)
                {
                    currentPosition += v.Length;
                }
            }
            // write sentinel
            Debug.Assert(currentPosition == maxAddress);
            w.Add(currentPosition);
            w.Finish();
        }

        private void AddFixedDerefBytesField(FieldInfo field, IndexOutput data, IndexOutput index, IEnumerable<BytesRef> values, int length)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_FIXED_DEREF.Name);

            CodecUtil.WriteHeader(data, Lucene40DocValuesFormat.BYTES_FIXED_DEREF_CODEC_NAME_DAT, Lucene40DocValuesFormat.BYTES_FIXED_DEREF_VERSION_CURRENT);

            CodecUtil.WriteHeader(index, Lucene40DocValuesFormat.BYTES_FIXED_DEREF_CODEC_NAME_IDX, Lucene40DocValuesFormat.BYTES_FIXED_DEREF_VERSION_CURRENT);

            // deduplicate
            SortedSet<BytesRef> dictionary = new SortedSet<BytesRef>();
            foreach (BytesRef v in values)
            {
                dictionary.Add(v == null ? new BytesRef() : BytesRef.DeepCopyOf(v));
            }

            /* values */
            data.WriteInt(length);
            foreach (BytesRef v in dictionary)
            {
                data.WriteBytes(v.Bytes, v.Offset, v.Length);
            }

            /* ordinals */
            int valueCount = dictionary.Count;
            Debug.Assert(valueCount > 0);
            index.WriteInt(valueCount);
            int maxDoc = State.SegmentInfo.DocCount;
            PackedInts.Writer w = PackedInts.GetWriter(index, maxDoc, PackedInts.BitsRequired(valueCount - 1), PackedInts.DEFAULT);

            BytesRef brefDummy;
            foreach (BytesRef v in values)
            {
                brefDummy = v;

                if (v == null)
                {
                    brefDummy = new BytesRef();
                }
                //int ord = dictionary.HeadSet(brefDummy).Size();
                int ord = dictionary.Count(@ref => @ref.CompareTo(brefDummy) < 0);
                w.Add(ord);
            }
            w.Finish();
        }

        private void AddVarDerefBytesField(FieldInfo field, IndexOutput data, IndexOutput index, IEnumerable<BytesRef> values)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_VAR_DEREF.Name);

            CodecUtil.WriteHeader(data, Lucene40DocValuesFormat.BYTES_VAR_DEREF_CODEC_NAME_DAT, Lucene40DocValuesFormat.BYTES_VAR_DEREF_VERSION_CURRENT);

            CodecUtil.WriteHeader(index, Lucene40DocValuesFormat.BYTES_VAR_DEREF_CODEC_NAME_IDX, Lucene40DocValuesFormat.BYTES_VAR_DEREF_VERSION_CURRENT);

            // deduplicate
            SortedSet<BytesRef> dictionary = new SortedSet<BytesRef>();
            foreach (BytesRef v in values)
            {
                dictionary.Add(v == null ? new BytesRef() : BytesRef.DeepCopyOf(v));
            }

            /* values */
            long startPosition = data.FilePointer;
            long currentAddress = 0;
            Dictionary<BytesRef, long> valueToAddress = new Dictionary<BytesRef, long>();
            foreach (BytesRef v in dictionary)
            {
                currentAddress = data.FilePointer - startPosition;
                valueToAddress[v] = currentAddress;
                WriteVShort(data, v.Length);
                data.WriteBytes(v.Bytes, v.Offset, v.Length);
            }

            /* ordinals */
            long totalBytes = data.FilePointer - startPosition;
            index.WriteLong(totalBytes);
            int maxDoc = State.SegmentInfo.DocCount;
            PackedInts.Writer w = PackedInts.GetWriter(index, maxDoc, PackedInts.BitsRequired(currentAddress), PackedInts.DEFAULT);

            foreach (BytesRef v in values)
            {
                w.Add(valueToAddress[v == null ? new BytesRef() : v]);
            }
            w.Finish();
        }

        // the little vint encoding used for var-deref
        private static void WriteVShort(IndexOutput o, int i)
        {
            Debug.Assert(i >= 0 && i <= short.MaxValue);
            if (i < 128)
            {
                o.WriteByte((sbyte)i);
            }
            else
            {
                o.WriteByte(unchecked((sbyte)(0x80 | (i >> 8))));
                o.WriteByte(unchecked((sbyte)(i & 0xff)));
            }
        }

        public override void AddSortedField(FieldInfo field, IEnumerable<BytesRef> values, IEnumerable<long> docToOrd)
        {
            // examine the values to determine best type to use
            int minLength = int.MaxValue;
            int maxLength = int.MinValue;
            foreach (BytesRef b in values)
            {
                minLength = Math.Min(minLength, b.Length);
                maxLength = Math.Max(maxLength, b.Length);
            }

            // but dont use fixed if there are missing values (we are simulating how lucene40 wrote dv...)
            bool anyMissing = false;
            foreach (long n in docToOrd)
            {
                if ((long)n == -1)
                {
                    anyMissing = true;
                    break;
                }
            }

            bool success = false;
            IndexOutput data = null;
            IndexOutput index = null;
            string dataName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "dat");
            string indexName = IndexFileNames.SegmentFileName(State.SegmentInfo.Name + "_" + Convert.ToString(field.Number), SegmentSuffix, "idx");

            try
            {
                data = Dir.CreateOutput(dataName, State.Context);
                index = Dir.CreateOutput(indexName, State.Context);
                if (minLength == maxLength && !anyMissing)
                {
                    // fixed byte[]
                    AddFixedSortedBytesField(field, data, index, values, docToOrd, minLength);
                }
                else
                {
                    // var byte[]
                    // three cases for simulating the old writer:
                    // 1. no missing
                    // 2. missing (and empty string in use): remap ord=-1 -> ord=0
                    // 3. missing (and empty string not in use): remap all ords +1, insert empty string into values
                    if (!anyMissing)
                    {
                        AddVarSortedBytesField(field, data, index, values, docToOrd);
                    }
                    else if (minLength == 0)
                    {
                        AddVarSortedBytesField(field, data, index, values, MissingOrdRemapper.MapMissingToOrd0(docToOrd));
                    }
                    else
                    {
                        AddVarSortedBytesField(field, data, index, MissingOrdRemapper.InsertEmptyValue(values), MissingOrdRemapper.MapAllOrds(docToOrd));
                    }
                }
                success = true;
            }
            finally
            {
                if (success)
                {
                    IOUtils.Close(data, index);
                }
                else
                {
                    IOUtils.CloseWhileHandlingException(data, index);
                }
            }
        }

        private void AddFixedSortedBytesField(FieldInfo field, IndexOutput data, IndexOutput index, IEnumerable<BytesRef> values, IEnumerable<long> docToOrd, int length)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_FIXED_SORTED.Name);

            CodecUtil.WriteHeader(data, Lucene40DocValuesFormat.BYTES_FIXED_SORTED_CODEC_NAME_DAT, Lucene40DocValuesFormat.BYTES_FIXED_SORTED_VERSION_CURRENT);

            CodecUtil.WriteHeader(index, Lucene40DocValuesFormat.BYTES_FIXED_SORTED_CODEC_NAME_IDX, Lucene40DocValuesFormat.BYTES_FIXED_SORTED_VERSION_CURRENT);

            /* values */

            data.WriteInt(length);
            int valueCount = 0;
            foreach (BytesRef v in values)
            {
                data.WriteBytes(v.Bytes, v.Offset, v.Length);
                valueCount++;
            }

            /* ordinals */

            index.WriteInt(valueCount);
            int maxDoc = State.SegmentInfo.DocCount;
            Debug.Assert(valueCount > 0);
            PackedInts.Writer w = PackedInts.GetWriter(index, maxDoc, PackedInts.BitsRequired(valueCount - 1), PackedInts.DEFAULT);
            foreach (long n in docToOrd)
            {
                w.Add((long)n);
            }
            w.Finish();
        }

        private void AddVarSortedBytesField(FieldInfo field, IndexOutput data, IndexOutput index, IEnumerable<BytesRef> values, IEnumerable<long> docToOrd)
        {
            field.PutAttribute(LegacyKey, LegacyDocValuesType.BYTES_VAR_SORTED.Name);

            CodecUtil.WriteHeader(data, Lucene40DocValuesFormat.BYTES_VAR_SORTED_CODEC_NAME_DAT, Lucene40DocValuesFormat.BYTES_VAR_SORTED_VERSION_CURRENT);

            CodecUtil.WriteHeader(index, Lucene40DocValuesFormat.BYTES_VAR_SORTED_CODEC_NAME_IDX, Lucene40DocValuesFormat.BYTES_VAR_SORTED_VERSION_CURRENT);

            /* values */

            long startPos = data.FilePointer;

            int valueCount = 0;
            foreach (BytesRef v in values)
            {
                data.WriteBytes(v.Bytes, v.Offset, v.Length);
                valueCount++;
            }

            /* addresses */

            long maxAddress = data.FilePointer - startPos;
            index.WriteLong(maxAddress);

            Debug.Assert(valueCount != int.MaxValue); // unsupported by the 4.0 impl

            PackedInts.Writer w = PackedInts.GetWriter(index, valueCount + 1, PackedInts.BitsRequired(maxAddress), PackedInts.DEFAULT);
            long currentPosition = 0;
            foreach (BytesRef v in values)
            {
                w.Add(currentPosition);
                currentPosition += v.Length;
            }
            // write sentinel
            Debug.Assert(currentPosition == maxAddress);
            w.Add(currentPosition);
            w.Finish();

            /* ordinals */

            int maxDoc = State.SegmentInfo.DocCount;
            Debug.Assert(valueCount > 0);
            PackedInts.Writer ords = PackedInts.GetWriter(index, maxDoc, PackedInts.BitsRequired(valueCount - 1), PackedInts.DEFAULT);
            foreach (long n in docToOrd)
            {
                ords.Add((long)n);
            }
            ords.Finish();
        }

        public override void AddSortedSetField(FieldInfo field, IEnumerable<BytesRef> values, IEnumerable<long> docToOrdCount, IEnumerable<long> ords)
        {
            throw new System.NotSupportedException("Lucene 4.0 does not support SortedSet docvalues");
        }

        protected override void Dispose(bool disposing)
        {
            Dir.Dispose();
        }
    }
}