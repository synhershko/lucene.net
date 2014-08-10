namespace Lucene.Net.Codecs.Lucene42
{
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

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

    using SegmentWriteState = Lucene.Net.Index.SegmentWriteState;

    /// <summary>
    /// Read-write version of <seealso cref="Lucene42DocValuesFormat"/> for testing.
    /// </summary>
    public class Lucene42RWDocValuesFormat : Lucene42DocValuesFormat
    {
        public override DocValuesConsumer FieldsConsumer(SegmentWriteState state)
        {
            if (!LuceneTestCase.OLD_FORMAT_IMPERSONATION_IS_ACTIVE)
            {
                return base.FieldsConsumer(state);
            }
            else
            {
                // note: we choose DEFAULT here (its reasonably fast, and for small bpv has tiny waste)
                return new Lucene42DocValuesConsumer(state, DATA_CODEC, DATA_EXTENSION, METADATA_CODEC, METADATA_EXTENSION, AcceptableOverheadRatio);
            }
        }
    }
}