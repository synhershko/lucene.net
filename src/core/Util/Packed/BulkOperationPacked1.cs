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
    internal sealed class BulkOperationPacked1 : BulkOperationPacked
    {
        public BulkOperationPacked1()
            : base(1)
        {
        }

        public override void Decode(long[] blocks, int blocksOffset, int[] values, int valuesOffset, int iterations)
        {
            for (int i = 0; i < iterations; ++i)
            {
                //JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
                //ORIGINAL LINE: final long block = blocks[blocksOffset++];
                long block = blocks[blocksOffset++];
                for (int shift = 63; shift >= 0; shift -= 1)
                {
                    values[valuesOffset++] = (int)(((long)((ulong)block >> shift)) & 1);
                }
            }
        }

        public override void Decode(sbyte[] blocks, int blocksOffset, int[] values, int valuesOffset, int iterations)
        {
            for (int j = 0; j < iterations; ++j)
            {
                //JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
                //ORIGINAL LINE: final byte block = blocks[blocksOffset++];
                sbyte block = blocks[blocksOffset++];
                values[valuesOffset++] = ((int)((uint)block >> 7)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 6)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 5)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 4)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 3)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 2)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 1)) & 1;
                values[valuesOffset++] = block & 1;
            }
        }

        public override void Decode(long[] blocks, int blocksOffset, long[] values, int valuesOffset, int iterations)
        {
            for (int i = 0; i < iterations; ++i)
            {
                //JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
                //ORIGINAL LINE: final long block = blocks[blocksOffset++];
                long block = blocks[blocksOffset++];
                for (int shift = 63; shift >= 0; shift -= 1)
                {
                    values[valuesOffset++] = ((long)((ulong)block >> shift)) & 1;
                }
            }
        }

        public override void Decode(sbyte[] blocks, int blocksOffset, long[] values, int valuesOffset, int iterations)
        {
            for (int j = 0; j < iterations; ++j)
            {
                //JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
                //ORIGINAL LINE: final byte block = blocks[blocksOffset++];
                sbyte block = blocks[blocksOffset++];
                values[valuesOffset++] = ((int)((uint)block >> 7)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 6)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 5)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 4)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 3)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 2)) & 1;
                values[valuesOffset++] = ((int)((uint)block >> 1)) & 1;
                values[valuesOffset++] = block & 1;
            }
        }
    }
}