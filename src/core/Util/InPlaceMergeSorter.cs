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
	/// <seealso cref="Sorter"/> implementation based on the merge-sort algorithm that merges
	///  in place (no extra memory will be allocated). Small arrays are sorted with
	///  insertion sort.
	///  @lucene.internal 
	/// </summary>
	public abstract class InPlaceMergeSorter : Sorter
	{

	  /// <summary>
	  /// Create a new <seealso cref="InPlaceMergeSorter"/> </summary>
	  public InPlaceMergeSorter()
	  {
	  }

	  public override sealed void Sort(int from, int to)
	  {
		CheckRange(from, to);
		MergeSort(from, to);
	  }

	  internal virtual void MergeSort(int from, int to)
	  {
		if (to - from < THRESHOLD)
		{
		  InsertionSort(from, to);
		}
		else
		{
//JAVA TO C# CONVERTER WARNING: The original Java variable was marked 'final':
//ORIGINAL LINE: final int mid = (from + to) >>> 1;
		  int mid = (int)((uint)(from + to) >> 1);
		  MergeSort(from, mid);
		  MergeSort(mid, to);
		  MergeInPlace(from, mid, to);
		}
	  }

	}

}