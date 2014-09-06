using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lucene.Net.Support;

namespace Lucene.Net.Util
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
    /// Class that Posting and PostingVector use to write byte
    /// streams into shared fixed-size byte[] arrays.  The idea
    /// is to allocate slices of increasing lengths For
    /// example, the first slice is 5 bytes, the next slice is
    /// 14, etc.  We start by writing our bytes into the first
    /// 5 bytes.  When we hit the end of the slice, we allocate
    /// the next slice and then write the address of the new
    /// slice into the last 4 bytes of the previous slice (the
    /// "forwarding address").
    ///
    /// Each slice is filled with 0's initially, and we mark
    /// the end with a non-zero byte.  this way the methods
    /// that are writing into the slice don't need to record
    /// its length and instead allocate a new slice once they
    /// hit a non-zero byte.
    ///
    /// @lucene.internal
    ///
    /// </summary>
    public sealed class ByteBlockPool
    {
        public const int BYTE_BLOCK_SHIFT = 15;
        public static readonly int BYTE_BLOCK_SIZE = 1 << BYTE_BLOCK_SHIFT;
        public static readonly int BYTE_BLOCK_MASK = BYTE_BLOCK_SIZE - 1;

        /// <summary>
        /// Abstract class for allocating and freeing byte
        ///  blocks.
        /// </summary>
        public abstract class Allocator
        {
            protected internal readonly int BlockSize;

            protected Allocator(int blockSize)
            {
                this.BlockSize = blockSize;
            }

            public abstract void RecycleByteBlocks(sbyte[][] blocks, int start, int end);

            public virtual void RecycleByteBlocks(List<sbyte[]> blocks)
            {
                var b = blocks.ToArray();
                RecycleByteBlocks(b, 0, b.Length);
            }

            public virtual sbyte[] ByteBlock
            {
                get
                {
                    return new sbyte[BlockSize];
                }
            }
        }

        /// <summary>
        /// A simple <seealso cref="Allocator"/> that never recycles. </summary>
        public sealed class DirectAllocator : Allocator
        {
            public DirectAllocator()
                : this(BYTE_BLOCK_SIZE)
            {
            }

            public DirectAllocator(int blockSize)
                : base(blockSize)
            {
            }

            public override void RecycleByteBlocks(sbyte[][] blocks, int start, int end)
            {
            }
        }

        /// <summary>
        /// A simple <seealso cref="Allocator"/> that never recycles, but
        ///  tracks how much total RAM is in use.
        /// </summary>
        public class DirectTrackingAllocator : Allocator
        {
            internal readonly Counter BytesUsed;

            public DirectTrackingAllocator(Counter bytesUsed)
                : this(BYTE_BLOCK_SIZE, bytesUsed)
            {
            }

            public DirectTrackingAllocator(int blockSize, Counter bytesUsed)
                : base(blockSize)
            {
                this.BytesUsed = bytesUsed;
            }

            public override sbyte[] ByteBlock
            {
                get
                {
                    BytesUsed.AddAndGet(BlockSize);
                    return new sbyte[BlockSize];
                }
            }

            public override void RecycleByteBlocks(sbyte[][] blocks, int start, int end)
            {
                BytesUsed.AddAndGet(-((end - start) * BlockSize));
                for (var i = start; i < end; i++)
                {
                    blocks[i] = null;
                }
            }
        }

        /// <summary>
        /// array of buffers currently used in the pool. Buffers are allocated if
        /// needed don't modify this outside of this class.
        /// </summary>
        public sbyte[][] Buffers = new sbyte[10][];

        /// <summary>
        /// index into the buffers array pointing to the current buffer used as the head </summary>
        private int BufferUpto = -1; // Which buffer we are upto

        /// <summary>
        /// Where we are in head buffer </summary>
        public int ByteUpto = BYTE_BLOCK_SIZE;

        /// <summary>
        /// Current head buffer
        /// </summary>
        public sbyte[] Buffer;

        /// <summary>
        /// Current head offset </summary>
        public int ByteOffset = -BYTE_BLOCK_SIZE;

        private readonly Allocator allocator;

        public ByteBlockPool(Allocator allocator)
        {
            this.allocator = allocator;
        }

        /// <summary>
        /// Resets the pool to its initial state reusing the first buffer and fills all
        /// buffers with <tt>0</tt> bytes before they reused or passed to
        /// <seealso cref="Allocator#recycleByteBlocks(byte[][], int, int)"/>. Calling
        /// <seealso cref="ByteBlockPool#nextBuffer()"/> is not needed after reset.
        /// </summary>
        public void Reset()
        {
            Reset(true, true);
        }

        /// <summary>
        /// Expert: Resets the pool to its initial state reusing the first buffer. Calling
        /// <seealso cref="ByteBlockPool#nextBuffer()"/> is not needed after reset. </summary>
        /// <param name="zeroFillBuffers"> if <code>true</code> the buffers are filled with <tt>0</tt>.
        ///        this should be set to <code>true</code> if this pool is used with slices. </param>
        /// <param name="reuseFirst"> if <code>true</code> the first buffer will be reused and calling
        ///        <seealso cref="ByteBlockPool#nextBuffer()"/> is not needed after reset iff the
        ///        block pool was used before ie. <seealso cref="ByteBlockPool#nextBuffer()"/> was called before. </param>
        public void Reset(bool zeroFillBuffers, bool reuseFirst)
        {
            if (BufferUpto != -1)
            {
                // We allocated at least one buffer

                if (zeroFillBuffers)
                {
                    for (int i = 0; i < BufferUpto; i++)
                    {
                        // Fully zero fill buffers that we fully used
                        //Array.Clear(Buffers[i], 0, Buffers[i].Length);
                        Arrays.Fill(Buffers[i], (sbyte)0);
                    }
                    // Partial zero fill the final buffer
                    //Array.Clear(Buffers[BufferUpto], 0, BufferUpto);
                    Arrays.Fill(Buffers[BufferUpto], 0, ByteUpto, (sbyte)0);
                }

                if (BufferUpto > 0 || !reuseFirst)
                {
                    int offset = reuseFirst ? 1 : 0;
                    // Recycle all but the first buffer
                    allocator.RecycleByteBlocks(Buffers, offset, 1 + BufferUpto);
                    //Array.Clear(Buffers, 0, Buffers.Length);
                    Arrays.Fill(Buffers, offset, 1 + BufferUpto, null);
                }
                if (reuseFirst)
                {
                    // Re-use the first buffer
                    BufferUpto = 0;
                    ByteUpto = 0;
                    ByteOffset = 0;
                    Buffer = Buffers[0];
                }
                else
                {
                    BufferUpto = -1;
                    ByteUpto = BYTE_BLOCK_SIZE;
                    ByteOffset = -BYTE_BLOCK_SIZE;
                    Buffer = null;
                }
            }
        }

        /// <summary>
        /// Advances the pool to its next buffer. this method should be called once
        /// after the constructor to initialize the pool. In contrast to the
        /// constructor a <seealso cref="ByteBlockPool#reset()"/> call will advance the pool to
        /// its first buffer immediately.
        /// </summary>
        public void NextBuffer()
        {
            if (1 + BufferUpto == Buffers.Length)
            {
                var newBuffers = new sbyte[ArrayUtil.Oversize(Buffers.Length + 1, RamUsageEstimator.NUM_BYTES_OBJECT_REF)][];
                Array.Copy(Buffers, 0, newBuffers, 0, Buffers.Length);
                Buffers = newBuffers;
            }
            Buffer = Buffers[1 + BufferUpto] = allocator.ByteBlock;
            BufferUpto++;

            ByteUpto = 0;
            ByteOffset += BYTE_BLOCK_SIZE;
        }

        /// <summary>
        /// Allocates a new slice with the given size.</summary>
        /// <seealso>ByteBlockPool#FIRST_LEVEL_SIZE</seealso>
        public int NewSlice(int size)
        {
            if (ByteUpto > BYTE_BLOCK_SIZE - size)
            {
                NextBuffer();
            }
            int upto = ByteUpto;
            ByteUpto += size;
            Buffer[ByteUpto - 1] = 16;
            return upto;
        }

        // Size of each slice.  These arrays should be at most 16
        // elements (index is encoded with 4 bits).  First array
        // is just a compact way to encode X+1 with a max.  Second
        // array is the length of each slice, ie first slice is 5
        // bytes, next slice is 14 bytes, etc.

        /// <summary>
        /// An array holding the offset into the <seealso cref="ByteBlockPool#LEVEL_SIZE_ARRAY"/>
        /// to quickly navigate to the next slice level.
        /// </summary>
        public static readonly int[] NEXT_LEVEL_ARRAY = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 9 };

        /// <summary>
        /// An array holding the level sizes for byte slices.
        /// </summary>
        public static readonly int[] LEVEL_SIZE_ARRAY = new int[] { 5, 14, 20, 30, 40, 40, 80, 80, 120, 200 };

        /// <summary>
        /// The first level size for new slices </summary>
        /// <seealso cref= ByteBlockPool#newSlice(int) </seealso>
        public static readonly int FIRST_LEVEL_SIZE = LEVEL_SIZE_ARRAY[0];

        /// <summary>
        /// Creates a new byte slice with the given starting size and
        /// returns the slices offset in the pool.
        /// </summary>
        public int AllocSlice(sbyte[] slice, int upto)
        {
            int level = slice[upto] & 15;
            int newLevel = NEXT_LEVEL_ARRAY[level];
            int newSize = LEVEL_SIZE_ARRAY[newLevel];

            // Maybe allocate another block
            if (ByteUpto > BYTE_BLOCK_SIZE - newSize)
            {
                NextBuffer();
            }

            int newUpto = ByteUpto;
            int offset = newUpto + ByteOffset;
            ByteUpto += newSize;

            // Copy forward the past 3 bytes (which we are about
            // to overwrite with the forwarding address):
            Buffer[newUpto] = slice[upto - 3];
            Buffer[newUpto + 1] = slice[upto - 2];
            Buffer[newUpto + 2] = slice[upto - 1];

            // Write forwarding address at end of last slice:
            slice[upto - 3] = (sbyte)Number.URShift(offset, 24);
            slice[upto - 2] = (sbyte)Number.URShift(offset, 16);
            slice[upto - 1] = (sbyte)Number.URShift(offset, 8);
            slice[upto] = (sbyte)offset;

            // Write new level:
            Buffer[ByteUpto - 1] = (sbyte)(16 | newLevel);

            return newUpto + 3;
        }

        // Fill in a BytesRef from term's length & bytes encoded in
        // byte block
        public void SetBytesRef(BytesRef term, int textStart)
        {
            var bytes = term.Bytes = Buffers[textStart >> BYTE_BLOCK_SHIFT];
            var pos = textStart & BYTE_BLOCK_MASK;
            if ((bytes[pos] & 0x80) == 0)
            {
                // length is 1 byte
                term.Length = bytes[pos];
                term.Offset = pos + 1;
            }
            else
            {
                // length is 2 bytes
                term.Length = (bytes[pos] & 0x7f) + ((bytes[pos + 1] & 0xff) << 7);
                term.Offset = pos + 2;
            }
            Debug.Assert(term.Length >= 0);
        }

        /// <summary>
        /// Appends the bytes in the provided <seealso cref="BytesRef"/> at
        /// the current position.
        /// </summary>
        public void Append(BytesRef bytes)
        {
            var length = bytes.Length;
            if (length == 0)
            {
                return;
            }
            int offset = bytes.Offset;
            int overflow = (length + ByteUpto) - BYTE_BLOCK_SIZE;
            do
            {
                if (overflow <= 0)
                {
                    Array.Copy(bytes.Bytes, offset, Buffer, ByteUpto, length);
                    ByteUpto += length;
                    break;
                }
                else
                {
                    int bytesToCopy = length - overflow;
                    if (bytesToCopy > 0)
                    {
                        Array.Copy(bytes.Bytes, offset, Buffer, ByteUpto, bytesToCopy);
                        offset += bytesToCopy;
                        length -= bytesToCopy;
                    }
                    NextBuffer();
                    overflow = overflow - BYTE_BLOCK_SIZE;
                }
            } while (true);
        }

        /// <summary>
        /// Reads bytes bytes out of the pool starting at the given offset with the given
        /// length into the given byte array at offset <tt>off</tt>.
        /// <p>Note: this method allows to copy across block boundaries.</p>
        /// </summary>
        public void ReadBytes(long offset, sbyte[] bytes, int off, int length)
        {
            if (length == 0)
            {
                return;
            }
            int bytesOffset = off;
            int bytesLength = length;
            int bufferIndex = (int)(offset >> BYTE_BLOCK_SHIFT);
            sbyte[] buffer = Buffers[bufferIndex];
            int pos = (int)(offset & BYTE_BLOCK_MASK);
            int overflow = (pos + length) - BYTE_BLOCK_SIZE;
            do
            {
                if (overflow <= 0)
                {
                    Array.Copy(buffer, pos, bytes, bytesOffset, bytesLength);
                    break;
                }
                else
                {
                    int bytesToCopy = length - overflow;
                    Array.Copy(buffer, pos, bytes, bytesOffset, bytesToCopy);
                    pos = 0;
                    bytesLength -= bytesToCopy;
                    bytesOffset += bytesToCopy;
                    buffer = Buffers[++bufferIndex];
                    overflow = overflow - BYTE_BLOCK_SIZE;
                }
            } while (true);
        }
    }
}