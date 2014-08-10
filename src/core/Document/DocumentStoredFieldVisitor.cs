using System.Collections.Generic;

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

    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using StoredFieldVisitor = Lucene.Net.Index.StoredFieldVisitor;

    /// <summary>
    /// A <seealso cref="StoredFieldVisitor"/> that creates a {@link
    ///  Document} containing all stored fields, or only specific
    ///  requested fields provided to <seealso cref="#DocumentStoredFieldVisitor(Set)"/>.
    ///  <p>
    ///  this is used by <seealso cref="IndexReader#document(int)"/> to load a
    ///  document.
    ///
    /// @lucene.experimental
    /// </summary>

    public class DocumentStoredFieldVisitor : StoredFieldVisitor
    {
        private readonly Document Doc = new Document();
        private readonly ISet<string> FieldsToAdd;

        /// <summary>
        /// Load only fields named in the provided <code>Set&lt;String&gt;</code>. </summary>
        /// <param name="fieldsToAdd"> Set of fields to load, or <code>null</code> (all fields). </param>
        public DocumentStoredFieldVisitor(ISet<string> fieldsToAdd)
        {
            this.FieldsToAdd = fieldsToAdd;
        }

        /// <summary>
        /// Load only fields named in the provided fields. </summary>
        public DocumentStoredFieldVisitor(params string[] fields)
        {
            FieldsToAdd = new HashSet<string>();
            foreach (string field in fields)
            {
                FieldsToAdd.Add(field);
            }
        }

        /// <summary>
        /// Load all stored fields. </summary>
        public DocumentStoredFieldVisitor()
        {
            this.FieldsToAdd = null;
        }

        public override void BinaryField(FieldInfo fieldInfo, sbyte[] value)
        {
            Doc.Add(new StoredField(fieldInfo.Name, value));
        }

        public override void StringField(FieldInfo fieldInfo, string value)
        {
            FieldType ft = new FieldType(TextField.TYPE_STORED);
            ft.StoreTermVectors = fieldInfo.HasVectors();
            ft.Indexed = fieldInfo.Indexed;
            ft.OmitNorms = fieldInfo.OmitsNorms();
            ft.IndexOptionsValue = fieldInfo.FieldIndexOptions;
            Doc.Add(new Field(fieldInfo.Name, value, ft));
        }

        public override void IntField(FieldInfo fieldInfo, int value)
        {
            Doc.Add(new StoredField(fieldInfo.Name, value));
        }

        public override void LongField(FieldInfo fieldInfo, long value)
        {
            Doc.Add(new StoredField(fieldInfo.Name, value));
        }

        public override void FloatField(FieldInfo fieldInfo, float value)
        {
            Doc.Add(new StoredField(fieldInfo.Name, value));
        }

        public override void DoubleField(FieldInfo fieldInfo, double value)
        {
            Doc.Add(new StoredField(fieldInfo.Name, value));
        }

        public override Status NeedsField(FieldInfo fieldInfo)
        {
            return FieldsToAdd == null || FieldsToAdd.Contains(fieldInfo.Name) ? Status.YES : Status.NO;
        }

        /// <summary>
        /// Retrieve the visited document. </summary>
        /// <returns> Document populated with stored fields. Note that only
        ///         the stored information in the field instances is valid,
        ///         data such as boosts, indexing options, term vector options,
        ///         etc is not set. </returns>
        public virtual Document Document
        {
            get
            {
                return Doc;
            }
        }
    }
}