using System;

namespace Lucene.Net.Codecs.Lucene3x
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
    using IndexFormatTooNewException = Lucene.Net.Index.IndexFormatTooNewException;
    using IndexFormatTooOldException = Lucene.Net.Index.IndexFormatTooOldException;
    using IndexInput = Lucene.Net.Store.IndexInput;
    using IOContext = Lucene.Net.Store.IOContext;
    using IOUtils = Lucene.Net.Util.IOUtils;

    /// <summary>
    /// @lucene.experimental </summary>
    /// @deprecated Only for reading existing 3.x indexes
    [Obsolete("Only for reading existing 3.x indexes")]
    internal class Lucene3xFieldInfosReader : FieldInfosReader
    {
        /// <summary>
        /// Extension of field infos </summary>
        internal const string FIELD_INFOS_EXTENSION = "fnm";

        // First used in 2.9; prior to 2.9 there was no format header
        internal const int FORMAT_START = -2;

        // First used in 3.4: omit only positional information
        internal const int FORMAT_OMIT_POSITIONS = -3;

        internal const int FORMAT_MINIMUM = FORMAT_START;
        internal const int FORMAT_CURRENT = FORMAT_OMIT_POSITIONS;
        internal const sbyte IS_INDEXED = 0x1;
        internal const sbyte STORE_TERMVECTOR = 0x2;
        internal const sbyte OMIT_NORMS = 0x10;
        internal const sbyte STORE_PAYLOADS = 0x20;
        internal const sbyte OMIT_TERM_FREQ_AND_POSITIONS = 0x40;
        internal const sbyte OMIT_POSITIONS = -128;

        public override FieldInfos Read(Directory directory, string segmentName, string segmentSuffix, IOContext iocontext)
        {
            string fileName = IndexFileNames.SegmentFileName(segmentName, "", FIELD_INFOS_EXTENSION);
            IndexInput input = directory.OpenInput(fileName, iocontext);

            bool success = false;
            try
            {
                int format = input.ReadVInt();

                if (format > FORMAT_MINIMUM)
                {
                    throw new IndexFormatTooOldException(input, format, FORMAT_MINIMUM, FORMAT_CURRENT);
                }
                if (format < FORMAT_CURRENT)
                {
                    throw new IndexFormatTooNewException(input, format, FORMAT_MINIMUM, FORMAT_CURRENT);
                }

                int size = input.ReadVInt(); //read in the size
                FieldInfo[] infos = new FieldInfo[size];

                for (int i = 0; i < size; i++)
                {
                    string name = input.ReadString();
                    int fieldNumber = i;
                    byte bits = input.ReadByte();
                    bool isIndexed = (bits & IS_INDEXED) != 0;
                    bool storeTermVector = (bits & STORE_TERMVECTOR) != 0;
                    bool omitNorms = (bits & OMIT_NORMS) != 0;
                    bool storePayloads = (bits & STORE_PAYLOADS) != 0;
                    FieldInfo.IndexOptions indexOptions;
                    if (!isIndexed)
                    {
                        indexOptions = default(FieldInfo.IndexOptions);
                    }
                    else if ((bits & OMIT_TERM_FREQ_AND_POSITIONS) != 0)
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_ONLY;
                    }
                    else if ((bits & OMIT_POSITIONS) != 0)
                    {
                        if (format <= FORMAT_OMIT_POSITIONS)
                        {
                            indexOptions = FieldInfo.IndexOptions.DOCS_AND_FREQS;
                        }
                        else
                        {
                            throw new CorruptIndexException("Corrupt fieldinfos, OMIT_POSITIONS set but format=" + format + " (resource: " + input + ")");
                        }
                    }
                    else
                    {
                        indexOptions = FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS;
                    }

                    // LUCENE-3027: past indices were able to write
                    // storePayloads=true when omitTFAP is also true,
                    // which is invalid.  We correct that, here:
                    if (indexOptions != FieldInfo.IndexOptions.DOCS_AND_FREQS_AND_POSITIONS)
                    {
                        storePayloads = false;
                    }
                    // LUCENE TO-DO
                    infos[i] = new FieldInfo(name, isIndexed, fieldNumber, storeTermVector, omitNorms, storePayloads, indexOptions, null, isIndexed && !omitNorms ? FieldInfo.DocValuesType_e.NUMERIC : default(FieldInfo.DocValuesType_e), CollectionsHelper.EmptyMap<string, string>());
                }

                if (input.FilePointer != input.Length())
                {
                    throw new CorruptIndexException("did not read all bytes from file \"" + fileName + "\": read " + input.FilePointer + " vs size " + input.Length() + " (resource: " + input + ")");
                }
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
    }
}