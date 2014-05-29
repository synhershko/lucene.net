using System;

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
	/// this interface is used to reflect contents of <seealso cref="AttributeSource"/> or <seealso cref="AttributeImpl"/>.
	/// </summary>
	public interface AttributeReflector
	{

	  /// <summary>
	  /// this method gets called for every property in an <seealso cref="AttributeImpl"/>/<seealso cref="AttributeSource"/>
	  /// passing the class name of the <seealso cref="Attribute"/>, a key and the actual value.
	  /// E.g., an invocation of <seealso cref="Lucene.Net.Analysis.tokenattributes.CharTermAttributeImpl#reflectWith"/>
	  /// would call this method once using {@code Lucene.Net.Analysis.tokenattributes.CharTermAttribute.class}
	  /// as attribute class, {@code "term"} as key and the actual value as a String.
	  /// </summary>
	  void Reflect(Type attClass, string key, object value);

	}

}