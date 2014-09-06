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

using System;
using Lucene.Net.Index;
using Lucene.Net.Util;

namespace Lucene.Net.Codecs.BlockTerms
{


    /// <summary>
    /// TODO
    ///   - allow for non-regular index intervals?  eg with a
    ///     long string of rare terms, you don't need such
    ///     frequent indexing
    /// 
    /// {@link BlockTermsReader} interacts with an instance of this class
    /// to manage its terms index.  The writer must accept
    /// indexed terms (many pairs of BytesRef text + long
    /// fileOffset), and then this reader must be able to
    /// retrieve the nearest index term to a provided term
    /// text. 
    ///
    ///  @lucene.experimental */
    /// </summary>
    public abstract class TermsIndexReaderBase : IDisposable
    {

        public abstract FieldIndexEnum GetFieldEnum(FieldInfo fieldInfo);

        public abstract void Dispose();

        public abstract bool SupportsOrd();

        public abstract int GetDivisor();

        /// <summary>
        /// Similar to TermsEnum, except, the only "metadata" it
        /// reports for a given indexed term is the long fileOffset
        /// into the main terms dictionary file.
        /// </summary>
        public abstract class FieldIndexEnum
        {

            /// <summary> 
            /// Seeks to "largest" indexed term that's <=
            ///  term; returns file pointer index (into the main
            /// terms index file) for that term 
            /// </summary>
            public abstract long Seek(BytesRef term);

            /** Returns -1 at end */
            public abstract long Next();

            public abstract BytesRef Term();

            /// <summary></summary>
            /// <remarks>Only implemented if {@link TermsIndexReaderBase.supportsOrd()} 
            /// returns true</remarks>
            /// <returns></returns>
            public abstract long Seek(long ord);

            /// <summary></summary>
            /// <remarks>Only implemented if {@link TermsIndexReaderBase.supportsOrd()} 
            /// returns true</remarks>
            /// <returns></returns>
            public abstract long Ord();
        }

        /// <summary>Returns approximate RAM bytes used</summary>
        public abstract long RamBytesUsed();
    }
}