namespace Lucene.Net.Codecs.Lucene40
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

    using IndexFileNames = Lucene.Net.Index.IndexFileNames;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using SegmentWriteState = Lucene.Net.Index.SegmentWriteState;

    /// <summary>
    /// Read-write version of <seealso cref="Lucene40NormsFormat"/> for testing </summary>
    public class Lucene40RWNormsFormat : Lucene40NormsFormat
    {
        public override DocValuesConsumer NormsConsumer(SegmentWriteState state)
        {
            if (!LuceneTestCase.OLD_FORMAT_IMPERSONATION_IS_ACTIVE)
            {
                return base.NormsConsumer(state);
            }
            else
            {
                string filename = IndexFileNames.SegmentFileName(state.SegmentInfo.Name, "nrm", IndexFileNames.COMPOUND_FILE_EXTENSION);
                return new Lucene40DocValuesWriter(state, filename, Lucene40FieldInfosReader.LEGACY_NORM_TYPE_KEY);
            }
        }
    }
}