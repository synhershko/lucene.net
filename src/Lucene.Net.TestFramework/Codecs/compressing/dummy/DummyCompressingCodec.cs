using System.Diagnostics;

namespace Lucene.Net.Codecs.Compressing.dummy
{
    using ArrayUtil = Lucene.Net.Util.ArrayUtil;
    using BytesRef = Lucene.Net.Util.BytesRef;

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

    using DataInput = Lucene.Net.Store.DataInput;
    using DataOutput = Lucene.Net.Store.DataOutput;

    /// <summary>
    /// CompressionCodec that does not compress data, useful for testing. </summary>
    // In its own package to make sure the oal.codecs.compressing classes are
    // visible enough to let people write their own CompressionMode
    public class DummyCompressingCodec : CompressingCodec
    {
        public static readonly CompressionMode DUMMY = new CompressionModeAnonymousInnerClassHelper();

        private class CompressionModeAnonymousInnerClassHelper : CompressionMode
        {
            public CompressionModeAnonymousInnerClassHelper()
            {
            }

            public override Compressor NewCompressor()
            {
                return DUMMY_COMPRESSOR;
            }

            public override Decompressor NewDecompressor()
            {
                return DUMMY_DECOMPRESSOR;
            }

            public override string ToString()
            {
                return "DUMMY";
            }
        }

        private static readonly Decompressor DUMMY_DECOMPRESSOR = new DecompressorAnonymousInnerClassHelper();

        private class DecompressorAnonymousInnerClassHelper : Decompressor
        {
            public DecompressorAnonymousInnerClassHelper()
            {
            }

            public override void Decompress(DataInput @in, int originalLength, int offset, int length, BytesRef bytes)
            {
                Debug.Assert(offset + length <= originalLength);
                if (bytes.Bytes.Length < originalLength)
                {
                    bytes.Bytes = new sbyte[ArrayUtil.Oversize(originalLength, 1)];
                }
                @in.ReadBytes(bytes.Bytes, 0, offset + length);
                bytes.Offset = offset;
                bytes.Length = length;
            }

            public override object Clone()
            {
                return this;
            }
        }

        private static readonly Compressor DUMMY_COMPRESSOR = new CompressorAnonymousInnerClassHelper();

        private class CompressorAnonymousInnerClassHelper : Compressor
        {
            public CompressorAnonymousInnerClassHelper()
            {
            }

            public override void Compress(sbyte[] bytes, int off, int len, DataOutput @out)
            {
                @out.WriteBytes(bytes, off, len);
            }
        }

        /// <summary>
        /// Constructor that allows to configure the chunk size. </summary>
        public DummyCompressingCodec(int chunkSize, bool withSegmentSuffix)
            : base("DummyCompressingStoredFields", withSegmentSuffix ? "DummyCompressingStoredFields" : "", DUMMY, chunkSize)
        {
        }

        /// <summary>
        /// Default constructor. </summary>
        public DummyCompressingCodec()
            : this(1 << 14, false)
        {
        }
    }
}