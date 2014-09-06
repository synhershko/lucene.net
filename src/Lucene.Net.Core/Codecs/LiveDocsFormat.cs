using System.Collections.Generic;

namespace Lucene.Net.Codecs
{
    using Bits = Lucene.Net.Util.Bits;
    using Directory = Lucene.Net.Store.Directory;
    using IOContext = Lucene.Net.Store.IOContext;
    using MutableBits = Lucene.Net.Util.MutableBits;

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

    using SegmentCommitInfo = Lucene.Net.Index.SegmentCommitInfo;

    /// <summary>
    /// Format for live/deleted documents
    /// @lucene.experimental
    /// </summary>
    public abstract class LiveDocsFormat
    {
        /// <summary>
        /// Sole constructor. (For invocation by subclass
        ///  constructors, typically implicit.)
        /// </summary>
        protected internal LiveDocsFormat()
        {
        }

        /// <summary>
        /// Creates a new MutableBits, with all bits set, for the specified size. </summary>
        public abstract MutableBits NewLiveDocs(int size);

        /// <summary>
        /// Creates a new mutablebits of the same bits set and size of existing. </summary>
        public abstract MutableBits NewLiveDocs(Bits existing);

        /// <summary>
        /// Read live docs bits. </summary>
        public abstract Bits ReadLiveDocs(Directory dir, SegmentCommitInfo info, IOContext context);

        /// <summary>
        /// Persist live docs bits.  Use {@link
        ///  SegmentCommitInfo#getNextDelGen} to determine the
        ///  generation of the deletes file you should write to.
        /// </summary>
        public abstract void WriteLiveDocs(MutableBits bits, Directory dir, SegmentCommitInfo info, int newDelCount, IOContext context);

        /// <summary>
        /// Records all files in use by this <seealso cref="SegmentCommitInfo"/> into the files argument. </summary>
        public abstract void Files(SegmentCommitInfo info, ICollection<string> files);
    }
}