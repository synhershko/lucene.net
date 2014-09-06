using System;
using System.Collections.Generic;

namespace Lucene.Net.Codecs.Lucene42
{
    using Lucene.Net.Support;

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

    using CorruptIndexException = Lucene.Net.Index.CorruptIndexException;
    using Directory = Lucene.Net.Store.Directory;
    using FieldInfo = Lucene.Net.Index.FieldInfo;
    using FieldInfos = Lucene.Net.Index.FieldInfos;
    using IndexFileNames = Lucene.Net.Index.IndexFileNames;
    using IndexInput = Lucene.Net.Store.IndexInput;
    using IOContext = Lucene.Net.Store.IOContext;
    using IOUtils = Lucene.Net.Util.IOUtils;

    /// <summary>
    /// Lucene 4.2 FieldInfos reader.
    ///
    /// @lucene.experimental </summary>
    /// @deprecated Only for reading old 4.2-4.5 segments
    /// <seealso cref= Lucene42FieldInfosFormat </seealso>
    [Obsolete("Only for reading old 4.2-4.5 segments")]
    internal sealed class Lucene42FieldInfosReader : FieldInfosReader
    {
        /// <summary>
        /// Sole constructor. </summary>
        public Lucene42FieldInfosReader()
        {
        }

        public override FieldInfos Read(Directory directory, string segmentName, string segmentSuffix, IOContext iocontext)
        {
            string fileName = IndexFileNames.SegmentFileName(segmentName, "", Lucene42FieldInfosFormat.EXTENSION);
            IndexInput input = directory.OpenInput(fileName, iocontext);

            bool success = false;
            try
            {
                CodecUtil.CheckHeader(input, Lucene42FieldInfosFormat.CODEC_NAME, Lucene42FieldInfosFormat.FORMAT_START, Lucene42FieldInfosFormat.FORMAT_CURRENT);

                int size = input.ReadVInt(); //read in the size
                FieldInfo[] infos = new FieldInfo[size];

                for (int i = 0; i < size; i++)
                {
                    string name = input.ReadString();
                    int fieldNumber = input.ReadVInt();
                    byte bits = input.ReadByte();
                    bool isIndexed = (bits & Lucene42FieldInfosFormat.IS_INDEXED) != 0;
                    bool storeTermVector = (bits & Lucene42FieldInfosFormat.STORE_TERMVECTOR) != 0;
                    bool omitNorms = (bits & Lucene42FieldInfosFormat.OMIT_NORMS) != 0;
                    bool storePayloads = (bits & Lucene42FieldInfosFormat.STORE_PAYLOADS) != 0;
                    FieldInfo.IndexOptions indexOptions;
                    if (!isIndexed)
                    {
                        indexOptions = default(FieldInfo.IndexOptions);
                    }
                    else if ((bits & Lucene42FieldInfosFormat.OMIT_TERM_FREQ_AND_POSITIONS) != 0)
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_ONLY;
                    }
                    else if ((bits & Lucene42FieldInfosFormat.OMIT_POSITIONS) != 0)
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_AND_FREQS;
                    }
                    else if ((bits & Lucene42FieldInfosFormat.STORE_OFFSETS_IN_POSTINGS) != 0)
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS_AND_OFFSETS;
                    }
                    else
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS;
                    }

                    // DV Types are packed in one byte
                    byte val = input.ReadByte();
                    FieldInfo.DocValuesType_e docValuesType = GetDocValuesType(input, (sbyte)(val & 0x0F));
                    FieldInfo.DocValuesType_e normsType = GetDocValuesType(input, (sbyte)(((int)((uint)val >> 4)) & 0x0F));
                    IDictionary<string, string> attributes = input.ReadStringStringMap();
                    infos[i] = new FieldInfo(name, isIndexed, fieldNumber, storeTermVector, omitNorms, storePayloads, indexOptions, docValuesType, normsType, CollectionsHelper.UnmodifiableMap(attributes));
                }

                CodecUtil.CheckEOF(input);
                FieldInfos fieldInfos = new FieldInfos(infos);
                success = true;
                return fieldInfos;
            }
            finally
            {
                if (success)
                {
                    input.Dispose();
                }
                else
                {
                    IOUtils.CloseWhileHandlingException(input);
                }
            }
        }

        private static FieldInfo.DocValuesType_e GetDocValuesType(IndexInput input, sbyte b)
        {
            if (b == 0)
            {
                return default(FieldInfo.DocValuesType_e);
            }
            else if (b == 1)
            {
                return FieldInfo.DocValuesType_e.NUMERIC;
            }
            else if (b == 2)
            {
                return FieldInfo.DocValuesType_e.BINARY;
            }
            else if (b == 3)
            {
                return FieldInfo.DocValuesType_e.SORTED;
            }
            else if (b == 4)
            {
                return FieldInfo.DocValuesType_e.SORTED_SET;
            }
            else
            {
                throw new CorruptIndexException("invalid docvalues byte: " + b + " (resource=" + input + ")");
            }
        }
    }
}