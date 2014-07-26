using System;
using System.Diagnostics;

// this file has been automatically generated, DO NOT EDIT

namespace Lucene.Net.Util.Packed
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
    /// Efficient sequential read/write of packed integers.
    /// </summary>
    internal abstract class BulkOperation : PackedInts.Decoder, PackedInts.Encoder
    {
        public abstract void Encode(int[] values, int valuesOffset, sbyte[] blocks, int blocksOffset, int iterations);

        public abstract void Encode(int[] values, int valuesOffset, long[] blocks, int blocksOffset, int iterations);

        public abstract void Encode(long[] values, int valuesOffset, sbyte[] blocks, int blocksOffset, int iterations);

        public abstract void Encode(long[] values, int valuesOffset, long[] blocks, int blocksOffset, int iterations);

        public abstract void Decode(sbyte[] blocks, int blocksOffset, int[] values, int valuesOffset, int iterations);

        public abstract void Decode(long[] blocks, int blocksOffset, int[] values, int valuesOffset, int iterations);

        public abstract void Decode(sbyte[] blocks, int blocksOffset, long[] values, int valuesOffset, int iterations);

        public abstract void Decode(long[] blocks, int blocksOffset, long[] values, int valuesOffset, int iterations);

        public abstract int ByteValueCount();

        public abstract int ByteBlockCount();

        public abstract int LongValueCount();

        public abstract int LongBlockCount();

        private static readonly BulkOperation[] PackedBulkOps = new BulkOperation[] { new BulkOperationPacked1(), new BulkOperationPacked2(), new BulkOperationPacked3(), new BulkOperationPacked4(), new BulkOperationPacked5(), new BulkOperationPacked6(), new BulkOperationPacked7(), new BulkOperationPacked8(), new BulkOperationPacked9(), new BulkOperationPacked10(), new BulkOperationPacked11(), new BulkOperationPacked12(), new BulkOperationPacked13(), new BulkOperationPacked14(), new BulkOperationPacked15(), new BulkOperationPacked16(), new BulkOperationPacked17(), new BulkOperationPacked18(), new BulkOperationPacked19(), new BulkOperationPacked20(), new BulkOperationPacked21(), new BulkOperationPacked22(), new BulkOperationPacked23(), new BulkOperationPacked24(), new BulkOperationPacked(25), new BulkOperationPacked(26), new BulkOperationPacked(27), new BulkOperationPacked(28), new BulkOperationPacked(29), new BulkOperationPacked(30), new BulkOperationPacked(31), new BulkOperationPacked(32), new BulkOperationPacked(33), new BulkOperationPacked(34), new BulkOperationPacked(35), new BulkOperationPacked(36), new BulkOperationPacked(37), new BulkOperationPacked(38), new BulkOperationPacked(39), new BulkOperationPacked(40), new BulkOperationPacked(41), new BulkOperationPacked(42), new BulkOperationPacked(43), new BulkOperationPacked(44), new BulkOperationPacked(45), new BulkOperationPacked(46), new BulkOperationPacked(47), new BulkOperationPacked(48), new BulkOperationPacked(49), new BulkOperationPacked(50), new BulkOperationPacked(51), new BulkOperationPacked(52), new BulkOperationPacked(53), new BulkOperationPacked(54), new BulkOperationPacked(55), new BulkOperationPacked(56), new BulkOperationPacked(57), new BulkOperationPacked(58), new BulkOperationPacked(59), new BulkOperationPacked(60), new BulkOperationPacked(61), new BulkOperationPacked(62), new BulkOperationPacked(63), new BulkOperationPacked(64) };

        // NOTE: this is sparse (some entries are null):
        private static readonly BulkOperation[] PackedSingleBlockBulkOps = new BulkOperation[] { new BulkOperationPackedSingleBlock(1), new BulkOperationPackedSingleBlock(2), new BulkOperationPackedSingleBlock(3), new BulkOperationPackedSingleBlock(4), new BulkOperationPackedSingleBlock(5), new BulkOperationPackedSingleBlock(6), new BulkOperationPackedSingleBlock(7), new BulkOperationPackedSingleBlock(8), new BulkOperationPackedSingleBlock(9), new BulkOperationPackedSingleBlock(10), null, new BulkOperationPackedSingleBlock(12), null, null, null, new BulkOperationPackedSingleBlock(16), null, null, null, null, new BulkOperationPackedSingleBlock(21), null, null, null, null, null, null, null, null, null, null, new BulkOperationPackedSingleBlock(32) };

        public static BulkOperation Of(PackedInts.Format format, int bitsPerValue)
        {
            if (format == PackedInts.Format.PACKED)
            {
                Debug.Assert(PackedBulkOps[bitsPerValue - 1] != null);
                return PackedBulkOps[bitsPerValue - 1];
            }
            else if (format == PackedInts.Format.PACKED_SINGLE_BLOCK)
            {
                Debug.Assert(PackedSingleBlockBulkOps[bitsPerValue - 1] != null);
                return PackedSingleBlockBulkOps[bitsPerValue - 1];
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        protected internal virtual int WriteLong(long block, sbyte[] blocks, int blocksOffset)
        {
            for (int j = 1; j <= 8; ++j)
            {
                blocks[blocksOffset++] = (sbyte)((long)((ulong)block >> (64 - (j << 3))));
            }
            return blocksOffset;
        }

        /// <summary>
        /// For every number of bits per value, there is a minimum number of
        /// blocks (b) / values (v) you need to write in order to reach the next block
        /// boundary:
        ///  - 16 bits per value -> b=2, v=1
        ///  - 24 bits per value -> b=3, v=1
        ///  - 50 bits per value -> b=25, v=4
        ///  - 63 bits per value -> b=63, v=8
        ///  - ...
        ///
        /// A bulk read consists in copying <code>iterations*v</code> values that are
        /// contained in <code>iterations*b</code> blocks into a <code>long[]</code>
        /// (higher values of <code>iterations</code> are likely to yield a better
        /// throughput) => this requires n * (b + 8v) bytes of memory.
        ///
        /// this method computes <code>iterations</code> as
        /// <code>ramBudget / (b + 8v)</code> (since a long is 8 bytes).
        /// </summary>
        public int ComputeIterations(int valueCount, int ramBudget)
        {
            //JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
            //ORIGINAL LINE: final int iterations = ramBudget / (byteBlockCount() + 8 * byteValueCount());
            int iterations = ramBudget / (ByteBlockCount() + 8 * ByteValueCount());
            if (iterations == 0)
            {
                // at least 1
                return 1;
            }
            else if ((iterations - 1) * ByteValueCount() >= valueCount)
            {
                // don't allocate for more than the size of the reader
                return (int)Math.Ceiling((double)valueCount / ByteValueCount());
            }
            else
            {
                return iterations;
            }
        }
    }
}