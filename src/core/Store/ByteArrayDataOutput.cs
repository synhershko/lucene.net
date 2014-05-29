using System;
using System.Diagnostics;

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

	using BytesRef = Lucene.Net.Util.BytesRef;

	/// <summary>
	/// DataOutput backed by a byte array.
	/// <b>WARNING:</b> this class omits most low-level checks,
	/// so be sure to test heavily with assertions enabled.
	/// @lucene.experimental
	/// </summary>
	public class ByteArrayDataOutput : DataOutput
	{
	  private sbyte[] Bytes;

	  private int Pos;
	  private int Limit;

	  public ByteArrayDataOutput(sbyte[] bytes)
	  {
		Reset(bytes);
	  }

	  public ByteArrayDataOutput(sbyte[] bytes, int offset, int len)
	  {
		Reset(bytes, offset, len);
	  }

	  public ByteArrayDataOutput()
	  {
		Reset(BytesRef.EMPTY_BYTES);
	  }

	  public virtual void Reset(sbyte[] bytes)
	  {
		Reset(bytes, 0, bytes.Length);
	  }

	  public virtual void Reset(sbyte[] bytes, int offset, int len)
	  {
		this.Bytes = bytes;
		Pos = offset;
		Limit = offset + len;
	  }

	  public virtual int Position
	  {
		  get
		  {
			return Pos;
		  }
	  }

	  public override void WriteByte(sbyte b)
	  {
		Debug.Assert(Pos < Limit);
		Bytes[Pos++] = b;
	  }

	  public override void WriteBytes(sbyte[] b, int offset, int length)
	  {
		Debug.Assert(Pos + length <= Limit);
		Array.Copy(b, offset, Bytes, Pos, length);
		Pos += length;
	  }
	}

}