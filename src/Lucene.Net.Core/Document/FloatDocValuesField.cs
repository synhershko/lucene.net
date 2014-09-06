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

    // javadocs
    // javadocs

    /// <summary>
    /// Syntactic sugar for encoding floats as NumericDocValues
    /// via <seealso cref="Float#floatToRawIntBits(float)"/>.
    /// <p>
    /// Per-document floating point values can be retrieved via
    /// <seealso cref="FieldCache#getFloats(AtomicReader, String, boolean)"/>.
    /// <p>
    /// <b>NOTE</b>: In most all cases this will be rather inefficient,
    /// requiring four bytes per document. Consider encoding floating
    /// point values yourself with only as much precision as you require.
    /// </summary>
    public class FloatDocValuesField : NumericDocValuesField
    {
        /// <summary>
        /// Creates a new DocValues field with the specified 32-bit float value </summary>
        /// <param name="name"> field name </param>
        /// <param name="value"> 32-bit float value </param>
        /// <exception cref="IllegalArgumentException"> if the field name is null </exception>
        public FloatDocValuesField(string name, float value)
            : base(name, Support.Single.FloatToIntBits(value))
        {
        }

        public override float FloatValue
        {
            set
            {
                base.LongValue = Support.Single.FloatToIntBits(value);
            }
        }

        public override long LongValue
        {
            set
            {
                throw new System.ArgumentException("cannot change value type from Float to Long");
            }
        }
    }
}