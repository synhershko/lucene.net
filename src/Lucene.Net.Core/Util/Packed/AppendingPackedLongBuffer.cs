using System;

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
    /// Utility class to buffer a list of signed longs in memory. this class only
    /// supports appending and is optimized for non-negative numbers with a uniform distribution over a fixed (limited) range
    ///
    /// @lucene.internal
    /// </summary>
    public sealed class AppendingPackedLongBuffer : AbstractAppendingLongBuffer
    {
        /// <summary>
        ///<seealso cref="AppendingPackedLongBuffer"/> </summary>
        /// <param name="initialPageCount">        the initial number of pages </param>
        /// <param name="pageSize">                the size of a single page </param>
        /// <param name="acceptableOverheadRatio"> an acceptable overhead ratio per value </param>
        public AppendingPackedLongBuffer(int initialPageCount, int pageSize, float acceptableOverheadRatio)
            : base(initialPageCount, pageSize, acceptableOverheadRatio)
        {
        }

        /// <summary>
        /// Create an <seealso cref="AppendingPackedLongBuffer"/> with initialPageCount=16,
        /// pageSize=1024 and acceptableOverheadRatio=<seealso cref="PackedInts#DEFAULT"/>
        /// </summary>
        public AppendingPackedLongBuffer()
            : this(16, 1024, PackedInts.DEFAULT)
        {
        }

        /// <summary>
        /// Create an <seealso cref="AppendingPackedLongBuffer"/> with initialPageCount=16,
        /// pageSize=1024
        /// </summary>
        public AppendingPackedLongBuffer(float acceptableOverheadRatio)
            : this(16, 1024, acceptableOverheadRatio)
        {
        }

        internal override long Get(int block, int element)
        {
            if (block == ValuesOff)
            {
                return Pending[element];
            }
            else
            {
                return Values[block].Get(element);
            }
        }

        internal override int Get(int block, int element, long[] arr, int off, int len)
        {
            if (block == ValuesOff)
            {
                int sysCopyToRead = Math.Min(len, PendingOff - element);
                Array.Copy(Pending, element, arr, off, sysCopyToRead);
                return sysCopyToRead;
            }
            else
            {
                /* packed block */
                return Values[block].Get(element, arr, off, len);
            }
        }

        internal override void PackPendingValues()
        {
            // compute max delta
            long minValue = Pending[0];
            long maxValue = Pending[0];
            for (int i = 1; i < PendingOff; ++i)
            {
                minValue = Math.Min(minValue, Pending[i]);
                maxValue = Math.Max(maxValue, Pending[i]);
            }

            // build a new packed reader
            int bitsRequired = minValue < 0 ? 64 : PackedInts.BitsRequired(maxValue);
            PackedInts.Mutable mutable = PackedInts.GetMutable(PendingOff, bitsRequired, AcceptableOverheadRatio);
            for (int i = 0; i < PendingOff; )
            {
                i += mutable.Set(i, Pending, i, PendingOff - i);
            }
            Values[ValuesOff] = mutable;
        }

        public override Iterator GetIterator()
        {
            return new Iterator(this);
        }
    }
}