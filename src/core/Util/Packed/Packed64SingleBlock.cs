using System;
using System.Diagnostics;

// this file has been automatically generated, DO NOT EDIT

namespace Lucene.Net.Util.Packed
{

	/*
	 * Licensed to the Apache Software Foundation (ASF) under one or more
	 * contributor license agreements. See the NOTICE file distributed with this
	 * work for additional information regarding copyright ownership. The ASF
	 * licenses this file to You under the Apache License, Version 2.0 (the
	 * "License"); you may not use this file except in compliance with the License.
	 * You may obtain a copy of the License at
	 *
	 * http://www.apache.org/licenses/LICENSE-2.0
	 *
	 * Unless required by applicable law or agreed to in writing, software
	 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
	 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
	 * License for the specific language governing permissions and limitations under
	 * the License.
	 */


	using DataInput = Lucene.Net.Store.DataInput;

	/// <summary>
	/// this class is similar to <seealso cref="Packed64"/> except that it trades space for
	/// speed by ensuring that a single block needs to be read/written in order to
	/// read/write a value.
	/// </summary>
	internal abstract class Packed64SingleBlock : PackedInts.MutableImpl
	{

	  public const int MAX_SUPPORTED_BITS_PER_VALUE = 32;
	  private static readonly int[] SUPPORTED_BITS_PER_VALUE = new int[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 16, 21, 32};

	  public static bool IsSupported(int bitsPerValue)
	  {
		return Arrays.binarySearch(SUPPORTED_BITS_PER_VALUE, bitsPerValue) >= 0;
	  }

	  private static int RequiredCapacity(int valueCount, int valuesPerBlock)
	  {
		return valueCount / valuesPerBlock + (valueCount % valuesPerBlock == 0 ? 0 : 1);
	  }

	  internal readonly long[] Blocks;

	  internal Packed64SingleBlock(int valueCount, int bitsPerValue) : base(valueCount, bitsPerValue)
	  {
		Debug.Assert(IsSupported(bitsPerValue));
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int valuesPerBlock = 64 / bitsPerValue;
		int valuesPerBlock = 64 / bitsPerValue;
		Blocks = new long[RequiredCapacity(valueCount, valuesPerBlock)];
	  }

	  public override void Clear()
	  {
		Arrays.fill(Blocks, 0L);
	  }

	  public override long RamBytesUsed()
	  {
		return RamUsageEstimator.AlignObjectSize(RamUsageEstimator.NUM_BYTES_OBJECT_HEADER + 2 * RamUsageEstimator.NUM_BYTES_INT + RamUsageEstimator.NUM_BYTES_OBJECT_REF) + RamUsageEstimator.SizeOf(Blocks); // blocks ref -  valueCount,bitsPerValue
	  }

	  public override int Get(int index, long[] arr, int off, int len)
	  {
		Debug.Assert(len > 0, "len must be > 0 (got " + len + ")");
		Debug.Assert(index >= 0 && index < ValueCount);
		len = Math.Min(len, ValueCount - index);
		Debug.Assert(off + len <= arr.Length);

//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int originalIndex = index;
		int originalIndex = index;

		// go to the next block boundary
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int valuesPerBlock = 64 / bitsPerValue;
		int valuesPerBlock = 64 / BitsPerValue_Renamed;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int offsetInBlock = index % valuesPerBlock;
		int offsetInBlock = index % valuesPerBlock;
		if (offsetInBlock != 0)
		{
		  for (int i = offsetInBlock; i < valuesPerBlock && len > 0; ++i)
		  {
			arr[off++] = Get(index++);
			--len;
		  }
		  if (len == 0)
		  {
			return index - originalIndex;
		  }
		}

		// bulk get
		Debug.Assert(index % valuesPerBlock == 0);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final PackedInts.Decoder decoder = BulkOperation.of(PackedInts.Format.PACKED_SINGLE_BLOCK, bitsPerValue);
		PackedInts.Decoder decoder = BulkOperation.Of(PackedInts.Format.PACKED_SINGLE_BLOCK, BitsPerValue_Renamed);
		Debug.Assert(decoder.LongBlockCount() == 1);
		Debug.Assert(decoder.LongValueCount() == valuesPerBlock);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int blockIndex = index / valuesPerBlock;
		int blockIndex = index / valuesPerBlock;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int nblocks = (index + len) / valuesPerBlock - blockIndex;
		int nblocks = (index + len) / valuesPerBlock - blockIndex;
		decoder.Decode(Blocks, blockIndex, arr, off, nblocks);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int diff = nblocks * valuesPerBlock;
		int diff = nblocks * valuesPerBlock;
		index += diff;
		len -= diff;

		if (index > originalIndex)
		{
		  // stay at the block boundary
		  return index - originalIndex;
		}
		else
		{
		  // no progress so far => already at a block boundary but no full block to
		  // get
		  Debug.Assert(index == originalIndex);
		  return base.Get(index, arr, off, len);
		}
	  }

	  public override int Set(int index, long[] arr, int off, int len)
	  {
		Debug.Assert(len > 0, "len must be > 0 (got " + len + ")");
		Debug.Assert(index >= 0 && index < ValueCount);
		len = Math.Min(len, ValueCount - index);
		Debug.Assert(off + len <= arr.Length);

//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int originalIndex = index;
		int originalIndex = index;

		// go to the next block boundary
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int valuesPerBlock = 64 / bitsPerValue;
		int valuesPerBlock = 64 / BitsPerValue_Renamed;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int offsetInBlock = index % valuesPerBlock;
		int offsetInBlock = index % valuesPerBlock;
		if (offsetInBlock != 0)
		{
		  for (int i = offsetInBlock; i < valuesPerBlock && len > 0; ++i)
		  {
			Set(index++, arr[off++]);
			--len;
		  }
		  if (len == 0)
		  {
			return index - originalIndex;
		  }
		}

		// bulk set
		Debug.Assert(index % valuesPerBlock == 0);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final BulkOperation op = BulkOperation.of(PackedInts.Format.PACKED_SINGLE_BLOCK, bitsPerValue);
		BulkOperation op = BulkOperation.Of(PackedInts.Format.PACKED_SINGLE_BLOCK, BitsPerValue_Renamed);
		Debug.Assert(op.LongBlockCount() == 1);
		Debug.Assert(op.LongValueCount() == valuesPerBlock);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int blockIndex = index / valuesPerBlock;
		int blockIndex = index / valuesPerBlock;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int nblocks = (index + len) / valuesPerBlock - blockIndex;
		int nblocks = (index + len) / valuesPerBlock - blockIndex;
		op.Encode(arr, off, Blocks, blockIndex, nblocks);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int diff = nblocks * valuesPerBlock;
		int diff = nblocks * valuesPerBlock;
		index += diff;
		len -= diff;

		if (index > originalIndex)
		{
		  // stay at the block boundary
		  return index - originalIndex;
		}
		else
		{
		  // no progress so far => already at a block boundary but no full block to
		  // set
		  Debug.Assert(index == originalIndex);
		  return base.Set(index, arr, off, len);
		}
	  }

	  public override void Fill(int fromIndex, int toIndex, long val)
	  {
		Debug.Assert(fromIndex >= 0);
		Debug.Assert(fromIndex <= toIndex);
		Debug.Assert(PackedInts.BitsRequired(val) <= BitsPerValue_Renamed);

//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int valuesPerBlock = 64 / bitsPerValue;
		int valuesPerBlock = 64 / BitsPerValue_Renamed;
		if (toIndex - fromIndex <= valuesPerBlock << 1)
		{
		  // there needs to be at least one full block to set for the block
		  // approach to be worth trying
		  base.Fill(fromIndex, toIndex, val);
		  return;
		}

		// set values naively until the next block start
		int fromOffsetInBlock = fromIndex % valuesPerBlock;
		if (fromOffsetInBlock != 0)
		{
		  for (int i = fromOffsetInBlock; i < valuesPerBlock; ++i)
		  {
			Set(fromIndex++, val);
		  }
		  Debug.Assert(fromIndex % valuesPerBlock == 0);
		}

		// bulk set of the inner blocks
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int fromBlock = fromIndex / valuesPerBlock;
		int fromBlock = fromIndex / valuesPerBlock;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int toBlock = toIndex / valuesPerBlock;
		int toBlock = toIndex / valuesPerBlock;
		Debug.Assert(fromBlock * valuesPerBlock == fromIndex);

		long blockValue = 0L;
		for (int i = 0; i < valuesPerBlock; ++i)
		{
		  blockValue = blockValue | (val << (i * BitsPerValue_Renamed));
		}
		Arrays.fill(Blocks, fromBlock, toBlock, blockValue);

		// fill the gap
		for (int i = valuesPerBlock * toBlock; i < toIndex; ++i)
		{
		  Set(i, val);
		}
	  }

	  protected internal override PackedInts.Format Format
	  {
		  get
		  {
			return PackedInts.Format.PACKED_SINGLE_BLOCK;
		  }
	  }

	  public override string ToString()
	  {
		return this.GetType().SimpleName + "(bitsPerValue=" + BitsPerValue_Renamed + ", size=" + Size() + ", elements.length=" + Blocks.Length + ")";
	  }

	  public static Packed64SingleBlock Create(DataInput @in, int valueCount, int bitsPerValue)
	  {
		Packed64SingleBlock reader = Create(valueCount, bitsPerValue);
		for (int i = 0; i < reader.Blocks.Length; ++i)
		{
		  reader.Blocks[i] = @in.ReadLong();
		}
		return reader;
	  }

	  public static Packed64SingleBlock Create(int valueCount, int bitsPerValue)
	  {
		switch (bitsPerValue)
		{
		  case 1:
			return new Packed64SingleBlock1(valueCount);
		  case 2:
			return new Packed64SingleBlock2(valueCount);
		  case 3:
			return new Packed64SingleBlock3(valueCount);
		  case 4:
			return new Packed64SingleBlock4(valueCount);
		  case 5:
			return new Packed64SingleBlock5(valueCount);
		  case 6:
			return new Packed64SingleBlock6(valueCount);
		  case 7:
			return new Packed64SingleBlock7(valueCount);
		  case 8:
			return new Packed64SingleBlock8(valueCount);
		  case 9:
			return new Packed64SingleBlock9(valueCount);
		  case 10:
			return new Packed64SingleBlock10(valueCount);
		  case 12:
			return new Packed64SingleBlock12(valueCount);
		  case 16:
			return new Packed64SingleBlock16(valueCount);
		  case 21:
			return new Packed64SingleBlock21(valueCount);
		  case 32:
			return new Packed64SingleBlock32(valueCount);
		  default:
			throw new System.ArgumentException("Unsupported number of bits per value: " + 32);
		}
	  }

	  internal class Packed64SingleBlock1 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock1(int valueCount) : base(valueCount, 1)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 6;
		  int o = (int)((uint)index >> 6);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 63;
		  int b = index & 63;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 0;
		  int shift = b << 0;
		  return ((long)((ulong)Blocks[o] >> shift)) & 1L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 6;
		  int o = (int)((uint)index >> 6);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 63;
		  int b = index & 63;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 0;
		  int shift = b << 0;
		  Blocks[o] = (Blocks[o] & ~(1L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock2 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock2(int valueCount) : base(valueCount, 2)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 5;
		  int o = (int)((uint)index >> 5);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 31;
		  int b = index & 31;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 1;
		  int shift = b << 1;
		  return ((long)((ulong)Blocks[o] >> shift)) & 3L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 5;
		  int o = (int)((uint)index >> 5);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 31;
		  int b = index & 31;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 1;
		  int shift = b << 1;
		  Blocks[o] = (Blocks[o] & ~(3L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock3 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock3(int valueCount) : base(valueCount, 3)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 21;
		  int o = index / 21;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 21;
		  int b = index % 21;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 3;
		  int shift = b * 3;
		  return ((long)((ulong)Blocks[o] >> shift)) & 7L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 21;
		  int o = index / 21;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 21;
		  int b = index % 21;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 3;
		  int shift = b * 3;
		  Blocks[o] = (Blocks[o] & ~(7L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock4 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock4(int valueCount) : base(valueCount, 4)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 4;
		  int o = (int)((uint)index >> 4);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 15;
		  int b = index & 15;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 2;
		  int shift = b << 2;
		  return ((long)((ulong)Blocks[o] >> shift)) & 15L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 4;
		  int o = (int)((uint)index >> 4);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 15;
		  int b = index & 15;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 2;
		  int shift = b << 2;
		  Blocks[o] = (Blocks[o] & ~(15L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock5 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock5(int valueCount) : base(valueCount, 5)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 12;
		  int o = index / 12;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 12;
		  int b = index % 12;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 5;
		  int shift = b * 5;
		  return ((long)((ulong)Blocks[o] >> shift)) & 31L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 12;
		  int o = index / 12;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 12;
		  int b = index % 12;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 5;
		  int shift = b * 5;
		  Blocks[o] = (Blocks[o] & ~(31L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock6 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock6(int valueCount) : base(valueCount, 6)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 10;
		  int o = index / 10;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 10;
		  int b = index % 10;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 6;
		  int shift = b * 6;
		  return ((long)((ulong)Blocks[o] >> shift)) & 63L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 10;
		  int o = index / 10;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 10;
		  int b = index % 10;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 6;
		  int shift = b * 6;
		  Blocks[o] = (Blocks[o] & ~(63L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock7 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock7(int valueCount) : base(valueCount, 7)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 9;
		  int o = index / 9;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 9;
		  int b = index % 9;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 7;
		  int shift = b * 7;
		  return ((long)((ulong)Blocks[o] >> shift)) & 127L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 9;
		  int o = index / 9;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 9;
		  int b = index % 9;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 7;
		  int shift = b * 7;
		  Blocks[o] = (Blocks[o] & ~(127L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock8 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock8(int valueCount) : base(valueCount, 8)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 3;
		  int o = (int)((uint)index >> 3);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 7;
		  int b = index & 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 3;
		  int shift = b << 3;
		  return ((long)((ulong)Blocks[o] >> shift)) & 255L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 3;
		  int o = (int)((uint)index >> 3);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 7;
		  int b = index & 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 3;
		  int shift = b << 3;
		  Blocks[o] = (Blocks[o] & ~(255L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock9 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock9(int valueCount) : base(valueCount, 9)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 7;
		  int o = index / 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 7;
		  int b = index % 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 9;
		  int shift = b * 9;
		  return ((long)((ulong)Blocks[o] >> shift)) & 511L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 7;
		  int o = index / 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 7;
		  int b = index % 7;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 9;
		  int shift = b * 9;
		  Blocks[o] = (Blocks[o] & ~(511L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock10 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock10(int valueCount) : base(valueCount, 10)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 6;
		  int o = index / 6;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 6;
		  int b = index % 6;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 10;
		  int shift = b * 10;
		  return ((long)((ulong)Blocks[o] >> shift)) & 1023L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 6;
		  int o = index / 6;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 6;
		  int b = index % 6;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 10;
		  int shift = b * 10;
		  Blocks[o] = (Blocks[o] & ~(1023L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock12 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock12(int valueCount) : base(valueCount, 12)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 5;
		  int o = index / 5;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 5;
		  int b = index % 5;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 12;
		  int shift = b * 12;
		  return ((long)((ulong)Blocks[o] >> shift)) & 4095L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 5;
		  int o = index / 5;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 5;
		  int b = index % 5;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 12;
		  int shift = b * 12;
		  Blocks[o] = (Blocks[o] & ~(4095L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock16 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock16(int valueCount) : base(valueCount, 16)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 2;
		  int o = (int)((uint)index >> 2);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 3;
		  int b = index & 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 4;
		  int shift = b << 4;
		  return ((long)((ulong)Blocks[o] >> shift)) & 65535L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 2;
		  int o = (int)((uint)index >> 2);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 3;
		  int b = index & 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 4;
		  int shift = b << 4;
		  Blocks[o] = (Blocks[o] & ~(65535L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock21 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock21(int valueCount) : base(valueCount, 21)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 3;
		  int o = index / 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 3;
		  int b = index % 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 21;
		  int shift = b * 21;
		  return ((long)((ulong)Blocks[o] >> shift)) & 2097151L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index / 3;
		  int o = index / 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index % 3;
		  int b = index % 3;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b * 21;
		  int shift = b * 21;
		  Blocks[o] = (Blocks[o] & ~(2097151L << shift)) | (value << shift);
		}

	  }

	  internal class Packed64SingleBlock32 : Packed64SingleBlock
	  {

		internal Packed64SingleBlock32(int valueCount) : base(valueCount, 32)
		{
		}

		public override long Get(int index)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 1;
		  int o = (int)((uint)index >> 1);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 1;
		  int b = index & 1;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 5;
		  int shift = b << 5;
		  return ((long)((ulong)Blocks[o] >> shift)) & 4294967295L;
		}

		public override void Set(int index, long value)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int o = index >>> 1;
		  int o = (int)((uint)index >> 1);
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int b = index & 1;
		  int b = index & 1;
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int shift = b << 5;
		  int shift = b << 5;
		  Blocks[o] = (Blocks[o] & ~(4294967295L << shift)) | (value << shift);
		}

	  }

	}

}