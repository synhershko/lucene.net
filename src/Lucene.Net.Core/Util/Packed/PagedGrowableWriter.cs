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

    using Mutable = Lucene.Net.Util.Packed.PackedInts.Mutable;

    /// <summary>
    /// A <seealso cref="PagedGrowableWriter"/>. this class slices data into fixed-size blocks
    /// which have independent numbers of bits per value and grow on-demand.
    /// <p>You should use this class instead of the <seealso cref="AbstractAppendingLongBuffer"/> related ones only when
    /// you need random write-access. Otherwise this class will likely be slower and
    /// less memory-efficient.
    /// @lucene.internal
    /// </summary>
    public sealed class PagedGrowableWriter : AbstractPagedMutable<PagedGrowableWriter>
    {
        internal readonly float AcceptableOverheadRatio;

        /// <summary>
        /// Create a new <seealso cref="PagedGrowableWriter"/> instance.
        /// </summary>
        /// <param name="size"> the number of values to store. </param>
        /// <param name="pageSize"> the number of values per page </param>
        /// <param name="startBitsPerValue"> the initial number of bits per value </param>
        /// <param name="acceptableOverheadRatio"> an acceptable overhead ratio </param>
        public PagedGrowableWriter(long size, int pageSize, int startBitsPerValue, float acceptableOverheadRatio)
            : this(size, pageSize, startBitsPerValue, acceptableOverheadRatio, true)
        {
        }

        internal PagedGrowableWriter(long size, int pageSize, int startBitsPerValue, float acceptableOverheadRatio, bool fillPages)
            : base(startBitsPerValue, size, pageSize)
        {
            this.AcceptableOverheadRatio = acceptableOverheadRatio;
            if (fillPages)
            {
                FillPages();
            }
        }

        protected internal override Mutable NewMutable(int valueCount, int bitsPerValue)
        {
            return new GrowableWriter(bitsPerValue, valueCount, AcceptableOverheadRatio);
        }

        protected internal override PagedGrowableWriter NewUnfilledCopy(long newSize)
        {
            return new PagedGrowableWriter(newSize, PageSize(), BitsPerValue, AcceptableOverheadRatio, false);
        }

        protected internal override long BaseRamBytesUsed()
        {
            return base.BaseRamBytesUsed() + RamUsageEstimator.NUM_BYTES_FLOAT;
        }
    }
}