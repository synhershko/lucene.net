using System;

namespace Lucene.Net.Document
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
    /// <p>
    /// Field that stores a per-document <code>byte</code> value for scoring,
    /// sorting or value retrieval. Here's an example usage:
    ///
    /// <pre class="prettyprint">
    ///   document.add(new ByteDocValuesField(name, (byte) 22));
    /// </pre>
    ///
    /// <p>
    /// If you also need to store the value, you should add a
    /// separate <seealso cref="StoredField"/> instance.
    /// </summary>
    /// <seealso cref= NumericDocValues </seealso>
    /// @deprecated use <seealso cref="NumericDocValuesField"/> instead.
    ///

    [Obsolete]
    public class ByteDocValuesField : NumericDocValuesField
    {
        /// <summary>
        /// Creates a new DocValues field with the specified 8-bit byte value </summary>
        /// <param name="name"> field name </param>
        /// <param name="value"> 8-bit byte value </param>
        /// <exception cref="IllegalArgumentException"> if the field name is null. </exception>
        public ByteDocValuesField(string name, sbyte value)
            : base(name, value)
        {
        }

        public override sbyte ByteValue
        {
            set
            {
                LongValue = value;
            }
        }
    }
}