using System;

namespace Lucene.Net.Codecs.Compressing
{
    using Lucene.Net.Randomized.Generators;
    using Lucene.Net.Support;
    using NUnit.Framework;

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

    using ByteArrayDataInput = Lucene.Net.Store.ByteArrayDataInput;
    using ByteArrayDataOutput = Lucene.Net.Store.ByteArrayDataOutput;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using TestUtil = Lucene.Net.Util.TestUtil;

    [TestFixture]
    public abstract class AbstractTestCompressionMode : LuceneTestCase
    {
        internal CompressionMode Mode;

        internal static sbyte[] RandomArray()
        {
            int max = Random().NextBoolean() ? Random().Next(4) : Random().Next(256);
            int length = Random().NextBoolean() ? Random().Next(20) : Random().Next(192 * 1024);
            return RandomArray(length, max);
        }

        internal static sbyte[] RandomArray(int length, int max)
        {
            sbyte[] arr = new sbyte[length];
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = (sbyte)RandomInts.NextIntBetween(Random(), 0, max);
            }
            return arr;
        }

        internal virtual sbyte[] Compress(sbyte[] decompressed, int off, int len)
        {
            Compressor compressor = Mode.NewCompressor();
            return Compress(compressor, decompressed, off, len);
        }

        internal static sbyte[] Compress(Compressor compressor, sbyte[] decompressed, int off, int len)
        {
            sbyte[] compressed = new sbyte[len * 2 + 16]; // should be enough
            ByteArrayDataOutput @out = new ByteArrayDataOutput((byte[])(Array)compressed);
            compressor.Compress(decompressed, off, len, @out);
            int compressedLen = @out.Position;
            return Arrays.CopyOf(compressed, compressedLen);
        }

        internal virtual sbyte[] Decompress(sbyte[] compressed, int originalLength)
        {
            Decompressor decompressor = Mode.NewDecompressor();
            return Decompress(decompressor, compressed, originalLength);
        }

        internal static sbyte[] Decompress(Decompressor decompressor, sbyte[] compressed, int originalLength)
        {
            BytesRef bytes = new BytesRef();
            decompressor.Decompress(new ByteArrayDataInput((byte[])(Array)compressed), originalLength, 0, originalLength, bytes);
            return Arrays.CopyOfRange(bytes.Bytes, bytes.Offset, bytes.Offset + bytes.Length);
        }

        internal virtual sbyte[] Decompress(sbyte[] compressed, int originalLength, int offset, int length)
        {
            Decompressor decompressor = Mode.NewDecompressor();
            BytesRef bytes = new BytesRef();
            decompressor.Decompress(new ByteArrayDataInput((byte[])(Array)compressed), originalLength, offset, length, bytes);
            return Arrays.CopyOfRange(bytes.Bytes, bytes.Offset, bytes.Offset + bytes.Length);
        }

        [Test]
        public virtual void TestDecompress()
        {
            int iterations = AtLeast(10);
            for (int i = 0; i < iterations; ++i)
            {
                sbyte[] decompressed = RandomArray();
                int off = Random().NextBoolean() ? 0 : TestUtil.NextInt(Random(), 0, decompressed.Length);
                int len = Random().NextBoolean() ? decompressed.Length - off : TestUtil.NextInt(Random(), 0, decompressed.Length - off);
                sbyte[] compressed = Compress(decompressed, off, len);
                sbyte[] restored = Decompress(compressed, len);
                Assert.AreEqual(Arrays.CopyOfRange(decompressed, off, off + len), restored);//was AssertArrayEquals
            }
        }

        [Test]
        public virtual void TestPartialDecompress()
        {
            int iterations = AtLeast(10);
            for (int i = 0; i < iterations; ++i)
            {
                sbyte[] decompressed = RandomArray();
                sbyte[] compressed = Compress(decompressed, 0, decompressed.Length);
                int offset, length;
                if (decompressed.Length == 0)
                {
                    offset = length = 0;
                }
                else
                {
                    offset = Random().Next(decompressed.Length);
                    length = Random().Next(decompressed.Length - offset);
                }
                sbyte[] restored = Decompress(compressed, decompressed.Length, offset, length);
                Assert.AreEqual(Arrays.CopyOfRange(decompressed, offset, offset + length), restored); //was AssertArrayEquals
            }
        }

        public virtual sbyte[] Test(sbyte[] decompressed)
        {
            return Test(decompressed, 0, decompressed.Length);
        }

        public virtual sbyte[] Test(sbyte[] decompressed, int off, int len)
        {
            sbyte[] compressed = Compress(decompressed, off, len);
            sbyte[] restored = Decompress(compressed, len);
            Assert.AreEqual(len, restored.Length);
            return compressed;
        }

        [Test]
        public virtual void TestEmptySequence()
        {
            Test(new sbyte[0]);
        }

        [Test]
        public virtual void TestShortSequence()
        {
            Test(new sbyte[] { (sbyte)Random().Next(256) });
        }

        [Test]
        public virtual void TestIncompressible()
        {
            sbyte[] decompressed = new sbyte[RandomInts.NextIntBetween(Random(), 20, 256)];
            for (int i = 0; i < decompressed.Length; ++i)
            {
                decompressed[i] = (sbyte)i;
            }
            Test(decompressed);
        }

        [Test]
        public virtual void TestConstant()
        {
            sbyte[] decompressed = new sbyte[TestUtil.NextInt(Random(), 1, 10000)];
            Arrays.Fill(decompressed, (sbyte)Random().Next());
            Test(decompressed);
        }

        [Test]
        public virtual void TestLUCENE5201()
        {
            sbyte[] data = { 14, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 72, 14, 72, 14, 85, 3, 72, 14, 72, 14, 72, 14, 72, 14, 72, 14, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 85, 3, 72, 14, 50, 64, 0, 46, -1, 0, 0, 0, 29, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 50, 64, 0, 47, -105, 0, 0, 0, 30, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, -97, 6, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 120, 64, 0, 48, 4, 0, 0, 0, 31, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 39, 24, 32, 34, 124, 0, 120, 64, 0, 48, 80, 0, 0, 0, 31, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 72, 34, 72, 29, 72, 37, 72, 35, 72, 45, 72, 23, 72, 46, 72, 20, 72, 40, 72, 33, 72, 25, 72, 39, 72, 38, 72, 26, 72, 28, 72, 42, 72, 24, 72, 27, 72, 36, 72, 41, 72, 32, 72, 18, 72, 30, 72, 22, 72, 31, 72, 43, 72, 19, 50, 64, 0, 49, 20, 0, 0, 0, 32, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 50, 64, 0, 50, 53, 0, 0, 0, 34, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 50, 64, 0, 51, 85, 0, 0, 0, 36, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, -97, 5, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 50, -64, 0, 51, -45, 0, 0, 0, 37, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -113, 0, 2, 3, -97, 6, 0, 68, -113, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 2, 3, 85, 8, -113, 0, 68, -97, 3, 0, 120, 64, 0, 52, -88, 0, 0, 0, 39, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 72, 13, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, 72, 13, 85, 5, 72, 13, -19, -24, -101, -35 };
            Test(data, 9, data.Length - 9);
        }
    }
}