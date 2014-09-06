using System;
using Lucene.Net.Index;

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
    // javadocs

    /// <summary>
    /// <p>
    /// Field that indexes <code>long</code> values
    /// for efficient range filtering and sorting. Here's an example usage:
    ///
    /// <pre class="prettyprint">
    /// document.add(new LongField(name, 6L, Field.Store.NO));
    /// </pre>
    ///
    /// For optimal performance, re-use the <code>LongField</code> and
    /// <seealso cref="Document"/> instance for more than one document:
    ///
    /// <pre class="prettyprint">
    ///  LongField field = new LongField(name, 0L, Field.Store.NO);
    ///  Document document = new Document();
    ///  document.add(field);
    ///
    ///  for(all documents) {
    ///    ...
    ///    field.setLongValue(value)
    ///    writer.addDocument(document);
    ///    ...
    ///  }
    /// </pre>
    ///
    /// See also <seealso cref="IntField"/>, <seealso cref="FloatField"/>, {@link
    /// DoubleField}.
    ///
    /// Any type that can be converted to long can also be
    /// indexed.  For example, date/time values represented by a
    /// <seealso cref="java.util.Date"/> can be translated into a long
    /// value using the <seealso cref="java.util.Date#getTime"/> method.  If you
    /// don't need millisecond precision, you can quantize the
    /// value, either by dividing the result of
    /// <seealso cref="java.util.Date#getTime"/> or using the separate getters
    /// (for year, month, etc.) to construct an <code>int</code> or
    /// <code>long</code> value.</p>
    ///
    /// <p>To perform range querying or filtering against a
    /// <code>LongField</code>, use <seealso cref="NumericRangeQuery"/> or {@link
    /// NumericRangeFilter}.  To sort according to a
    /// <code>LongField</code>, use the normal numeric sort types, eg
    /// <seealso cref="Lucene.Net.Search.SortField.Type#LONG"/>. <code>LongField</code>
    /// values can also be loaded directly from <seealso cref="FieldCache"/>.</p>
    ///
    /// <p>You may add the same field name as an <code>LongField</code> to
    /// the same document more than once.  Range querying and
    /// filtering will be the logical OR of all values; so a range query
    /// will hit all documents that have at least one value in
    /// the range. However sort behavior is not defined.  If you need to sort,
    /// you should separately index a single-valued <code>LongField</code>.</p>
    ///
    /// <p>A <code>LongField</code> will consume somewhat more disk space
    /// in the index than an ordinary single-valued field.
    /// However, for a typical index that includes substantial
    /// textual content per document, this increase will likely
    /// be in the noise. </p>
    ///
    /// <p>Within Lucene, each numeric value is indexed as a
    /// <em>trie</em> structure, where each term is logically
    /// assigned to larger and larger pre-defined brackets (which
    /// are simply lower-precision representations of the value).
    /// The step size between each successive bracket is called the
    /// <code>precisionStep</code>, measured in bits.  Smaller
    /// <code>precisionStep</code> values result in larger number
    /// of brackets, which consumes more disk space in the index
    /// but may result in faster range search performance.  The
    /// default value, 4, was selected for a reasonable tradeoff
    /// of disk space consumption versus performance.  You can
    /// create a custom <seealso cref="FieldType"/> and invoke the {@link
    /// FieldType#setNumericPrecisionStep} method if you'd
    /// like to change the value.  Note that you must also
    /// specify a congruent value when creating {@link
    /// NumericRangeQuery} or <seealso cref="NumericRangeFilter"/>.
    /// For low cardinality fields larger precision steps are good.
    /// If the cardinality is &lt; 100, it is fair
    /// to use <seealso cref="Integer#MAX_VALUE"/>, which produces one
    /// term per value.
    ///
    /// <p>For more information on the internals of numeric trie
    /// indexing, including the <a
    /// href="../search/NumericRangeQuery.html#precisionStepDesc"><code>precisionStep</code></a>
    /// configuration, see <seealso cref="NumericRangeQuery"/>. The format of
    /// indexed values is described in <seealso cref="NumericUtils"/>.
    ///
    /// <p>If you only need to sort by numeric value, and never
    /// run range querying/filtering, you can index using a
    /// <code>precisionStep</code> of <seealso cref="Integer#MAX_VALUE"/>.
    /// this will minimize disk space consumed. </p>
    ///
    /// <p>More advanced users can instead use {@link
    /// NumericTokenStream} directly, when indexing numbers. this
    /// class is a wrapper around this token stream type for
    /// easier, more intuitive usage.</p>
    ///
    /// @since 2.9
    /// </summary>

    public sealed class LongField : Field
    {
        /// <summary>
        /// Type for a LongField that is not stored:
        /// normalization factors, frequencies, and positions are omitted.
        /// </summary>
        public static readonly FieldType TYPE_NOT_STORED = new FieldType();

        static LongField()
        {
            TYPE_NOT_STORED.Indexed = true;
            TYPE_NOT_STORED.Tokenized = true;
            TYPE_NOT_STORED.OmitNorms = true;
            TYPE_NOT_STORED.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_ONLY;
            TYPE_NOT_STORED.NumericTypeValue = Lucene.Net.Document.FieldType.NumericType.LONG;
            TYPE_NOT_STORED.Freeze();
            TYPE_STORED.Indexed = true;
            TYPE_STORED.Tokenized = true;
            TYPE_STORED.OmitNorms = true;
            TYPE_STORED.IndexOptionsValue = FieldInfo.IndexOptions.DOCS_ONLY;
            TYPE_STORED.NumericTypeValue = Lucene.Net.Document.FieldType.NumericType.LONG;
            TYPE_STORED.Stored = true;
            TYPE_STORED.Freeze();
        }

        /// <summary>
        /// Type for a stored LongField:
        /// normalization factors, frequencies, and positions are omitted.
        /// </summary>
        public static readonly FieldType TYPE_STORED = new FieldType();

        /// <summary>
        /// Creates a stored or un-stored LongField with the provided value
        ///  and default <code>precisionStep</code> {@link
        ///  NumericUtils#PRECISION_STEP_DEFAULT} (4). </summary>
        ///  <param name="name"> field name </param>
        ///  <param name="value"> 64-bit long value </param>
        ///  <param name="stored"> Store.YES if the content should also be stored </param>
        ///  <exception cref="IllegalArgumentException"> if the field name is null. </exception>
        public LongField(string name, long value, Store stored)
            : base(name, stored == Store.YES ? TYPE_STORED : TYPE_NOT_STORED)
        {
            FieldsData = Convert.ToInt64(value);
        }

        /// <summary>
        /// Expert: allows you to customize the {@link
        ///  FieldType}. </summary>
        ///  <param name="name"> field name </param>
        ///  <param name="value"> 64-bit long value </param>
        ///  <param name="type"> customized field type: must have <seealso cref="FieldType#numericType()"/>
        ///         of <seealso cref="FieldType.NumericType#LONG"/>. </param>
        ///  <exception cref="IllegalArgumentException"> if the field name or type is null, or
        ///          if the field type does not have a LONG numericType() </exception>
        public LongField(string name, long value, FieldType type)
            : base(name, type)
        {
            if (type.NumericTypeValue != Lucene.Net.Document.FieldType.NumericType.LONG)
            {
                throw new System.ArgumentException("type.numericType() must be LONG but got " + type.NumericTypeValue);
            }
            FieldsData = Convert.ToInt64(value);
        }
    }
}