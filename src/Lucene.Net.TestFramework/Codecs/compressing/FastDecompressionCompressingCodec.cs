namespace Lucene.Net.Codecs.Compressing
{
    using Lucene42NormsFormat = Lucene.Net.Codecs.Lucene42.Lucene42NormsFormat;
    using PackedInts = Lucene.Net.Util.Packed.PackedInts;

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
    /// CompressionCodec that uses <seealso cref="CompressionMode#FAST_DECOMPRESSION"/> </summary>
    public class FastDecompressionCompressingCodec : CompressingCodec
    {
        /// <summary>
        /// Constructor that allows to configure the chunk size. </summary>
        public FastDecompressionCompressingCodec(int chunkSize, bool withSegmentSuffix)
            : base("FastDecompressionCompressingStoredFields", withSegmentSuffix ? "FastDecompressionCompressingStoredFields" : "", CompressionMode.FAST_DECOMPRESSION, chunkSize)
        {
        }

        /// <summary>
        /// Default constructor. </summary>
        public FastDecompressionCompressingCodec()
            : this(1 << 14, false)
        {
        }

        public override NormsFormat NormsFormat()
        {
            return new Lucene42NormsFormat(PackedInts.DEFAULT);
        }
    }
}