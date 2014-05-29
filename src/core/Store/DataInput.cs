using System;
using System.Diagnostics;
using System.Collections.Generic;
using Lucene.Net.Util;

namespace Lucene.Net.Store
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
	/// Abstract base class for performing read operations of Lucene's low-level
	/// data types.
	/// 
	/// <p>{@code DataInput} may only be used from one thread, because it is not
	/// thread safe (it keeps internal state like file position). To allow
	/// multithreaded use, every {@code DataInput} instance must be cloned before
	/// used in another thread. Subclasses must therefore implement <seealso cref="#clone()"/>,
	/// returning a new {@code DataInput} which operates on the same underlying
	/// resource, but positioned independently.
	/// </summary>
	public abstract class DataInput : ICloneable
	{

	  private const int SKIP_BUFFER_SIZE = 1024;

	  /* this buffer is used to skip over bytes with the default implementation of
	   * skipBytes. The reason why we need to use an instance member instead of
	   * sharing a single instance across threads is that some delegating
	   * implementations of DataInput might want to reuse the provided buffer in
	   * order to eg. update the checksum. If we shared the same buffer across
	   * threads, then another thread might update the buffer while the checksum is
	   * being computed, making it invalid. See LUCENE-5583 for more information.
	   */
	  private sbyte[] SkipBuffer;

	  /// <summary>
	  /// Reads and returns a single byte. </summary>
	  /// <seealso cref= DataOutput#writeByte(byte) </seealso>
	  public abstract sbyte ReadByte();

	  /// <summary>
	  /// Reads a specified number of bytes into an array at the specified offset. </summary>
	  /// <param name="b"> the array to read bytes into </param>
	  /// <param name="offset"> the offset in the array to start storing bytes </param>
	  /// <param name="len"> the number of bytes to read </param>
	  /// <seealso cref= DataOutput#writeBytes(byte[],int) </seealso>
	  public abstract void ReadBytes(sbyte[] b, int offset, int len);

	  /// <summary>
	  /// Reads a specified number of bytes into an array at the
	  /// specified offset with control over whether the read
	  /// should be buffered (callers who have their own buffer
	  /// should pass in "false" for useBuffer).  Currently only
	  /// <seealso cref="BufferedIndexInput"/> respects this parameter. </summary>
	  /// <param name="b"> the array to read bytes into </param>
	  /// <param name="offset"> the offset in the array to start storing bytes </param>
	  /// <param name="len"> the number of bytes to read </param>
	  /// <param name="useBuffer"> set to false if the caller will handle
	  /// buffering. </param>
	  /// <seealso cref= DataOutput#writeBytes(byte[],int) </seealso>
	  public virtual void ReadBytes(sbyte[] b, int offset, int len, bool useBuffer)
	  {
		// Default to ignoring useBuffer entirely
		ReadBytes(b, offset, len);
	  }

	  /// <summary>
	  /// Reads two bytes and returns a short. </summary>
	  /// <seealso cref= DataOutput#writeByte(byte) </seealso>
	  public virtual short ReadShort()
	  {
		return (short)(((ReadByte() & 0xFF) << 8) | (ReadByte() & 0xFF));
	  }

	  /// <summary>
	  /// Reads four bytes and returns an int. </summary>
	  /// <seealso cref= DataOutput#writeInt(int) </seealso>
	  public virtual int ReadInt()
	  {
		return ((ReadByte() & 0xFF) << 24) | ((ReadByte() & 0xFF) << 16) | ((ReadByte() & 0xFF) << 8) | (ReadByte() & 0xFF);
	  }

	  /// <summary>
	  /// Reads an int stored in variable-length format.  Reads between one and
	  /// five bytes.  Smaller values take fewer bytes.  Negative numbers are not
	  /// supported.
	  /// <p>
	  /// The format is described further in <seealso cref="DataOutput#writeVInt(int)"/>.
	  /// </summary>
	  /// <seealso cref= DataOutput#writeVInt(int) </seealso>
	  public virtual int ReadVInt()
	  {
		/* this is the original code of this method,
		 * but a Hotspot bug (see LUCENE-2975) corrupts the for-loop if
		 * readByte() is inlined. So the loop was unwinded!
		byte b = readByte();
		int i = b & 0x7F;
		for (int shift = 7; (b & 0x80) != 0; shift += 7) {
		  b = readByte();
		  i |= (b & 0x7F) << shift;
		}
		return i;
		*/
		sbyte b = ReadByte();
		if (b >= 0)
		{
			return b;
		}
		int i = b & 0x7F;
		b = ReadByte();
		i |= (b & 0x7F) << 7;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7F) << 14;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7F) << 21;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		// Warning: the next ands use 0x0F / 0xF0 - beware copy/paste errors:
		i |= (b & 0x0F) << 28;
		if ((b & 0xF0) == 0)
		{
			return i;
		}
		throw new System.IO.IOException("Invalid vInt detected (too many bits)");
	  }

	  /// <summary>
	  /// Reads eight bytes and returns a long. </summary>
	  /// <seealso cref= DataOutput#writeLong(long) </seealso>
	  public virtual long ReadLong()
	  {
		return (((long)ReadInt()) << 32) | (ReadInt() & 0xFFFFFFFFL);
	  }

	  /// <summary>
	  /// Reads a long stored in variable-length format.  Reads between one and
	  /// nine bytes.  Smaller values take fewer bytes.  Negative numbers are not
	  /// supported.
	  /// <p>
	  /// The format is described further in <seealso cref="DataOutput#writeVInt(int)"/>.
	  /// </summary>
	  /// <seealso cref= DataOutput#writeVLong(long) </seealso>
	  public virtual long ReadVLong()
	  {
		/* this is the original code of this method,
		 * but a Hotspot bug (see LUCENE-2975) corrupts the for-loop if
		 * readByte() is inlined. So the loop was unwinded!
		byte b = readByte();
		long i = b & 0x7F;
		for (int shift = 7; (b & 0x80) != 0; shift += 7) {
		  b = readByte();
		  i |= (b & 0x7FL) << shift;
		}
		return i;
		*/
		sbyte b = ReadByte();
		if (b >= 0)
		{
			return b;
		}
		long i = b & 0x7FL;
		b = ReadByte();
		i |= (b & 0x7FL) << 7;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 14;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 21;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 28;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 35;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 42;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 49;
		if (b >= 0)
		{
			return i;
		}
		b = ReadByte();
		i |= (b & 0x7FL) << 56;
		if (b >= 0)
		{
			return i;
		}
		throw new System.IO.IOException("Invalid vLong detected (negative values disallowed)");
	  }

	  /// <summary>
	  /// Reads a string. </summary>
	  /// <seealso cref= DataOutput#writeString(String) </seealso>
	  public virtual string ReadString()
	  {
		int length = ReadVInt();
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final byte[] bytes = new byte[length];
		sbyte[] bytes = new sbyte[length];
		ReadBytes(bytes, 0, length);
		return new string(bytes, 0, length, IOUtils.CHARSET_UTF_8);
	  }

	  /// <summary>
	  /// Returns a clone of this stream.
	  /// 
	  /// <p>Clones of a stream access the same data, and are positioned at the same
	  /// point as the stream they were cloned from.
	  /// 
	  /// <p>Expert: Subclasses must ensure that clones may be positioned at
	  /// different points in the input from each other and from the stream they
	  /// were cloned from.
	  /// </summary>
	  public override DataInput Clone()
	  {
		try
		{
		  return (DataInput) base.Clone();
		}
		catch (CloneNotSupportedException e)
		{
		  throw new Exception("this cannot happen: Failing to clone DataInput");
		}
	  }

	  /// <summary>
	  /// Reads a Map&lt;String,String&gt; previously written
	  ///  with <seealso cref="DataOutput#writeStringStringMap(Map)"/>. 
	  /// </summary>
	  public virtual IDictionary<string, string> ReadStringStringMap()
	  {
		IDictionary<string, string> map = new Dictionary<string, string>();
		int count = ReadInt();
		for (int i = 0;i < count;i++)
		{
		  string key = ReadString();
		  string val = ReadString();
		  map[key] = val;
		}

		return map;
	  }

	  /// <summary>
	  /// Reads a Set&lt;String&gt; previously written
	  ///  with <seealso cref="DataOutput#writeStringSet(Set)"/>. 
	  /// </summary>
	  public virtual ISet<string> ReadStringSet()
	  {
		ISet<string> set = new HashSet<string>();
		int count = ReadInt();
		for (int i = 0;i < count;i++)
		{
		  set.Add(ReadString());
		}

		return set;
	  }

	  /// <summary>
	  /// Skip over <code>numBytes</code> bytes. The contract on this method is that it
	  /// should have the same behavior as reading the same number of bytes into a
	  /// buffer and discarding its content. Negative values of <code>numBytes</code>
	  /// are not supported.
	  /// </summary>
	  public virtual void SkipBytes(long numBytes)
	  {
		if (numBytes < 0)
		{
		  throw new System.ArgumentException("numBytes must be >= 0, got " + numBytes);
		}
		if (SkipBuffer == null)
		{
		  SkipBuffer = new sbyte[SKIP_BUFFER_SIZE];
		}
		Debug.Assert(SkipBuffer.Length == SKIP_BUFFER_SIZE);
		for (long skipped = 0; skipped < numBytes;)
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int step = (int) Math.min(SKIP_BUFFER_SIZE, numBytes - skipped);
		  int step = (int) Math.Min(SKIP_BUFFER_SIZE, numBytes - skipped);
		  ReadBytes(SkipBuffer, 0, step, false);
		  skipped += step;
		}
	  }

	}

}