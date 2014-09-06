namespace Lucene.Net.Codecs
{
    using Directory = Lucene.Net.Store.Directory;
    using IOContext = Lucene.Net.Store.IOContext;

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

    using SegmentInfo = Lucene.Net.Index.SegmentInfo;

    /// <summary>
    /// Specifies an API for classes that can read <seealso cref="SegmentInfo"/> information.
    /// @lucene.experimental
    /// </summary>

    public abstract class SegmentInfoReader
    {
        /// <summary>
        /// Sole constructor. (For invocation by subclass
        ///  constructors, typically implicit.)
        /// </summary>
        protected internal SegmentInfoReader()
        {
        }

        /// <summary>
        /// Read <seealso cref="SegmentInfo"/> data from a directory. </summary>
        /// <param name="directory"> directory to read from </param>
        /// <param name="segmentName"> name of the segment to read </param>
        /// <returns> infos instance to be populated with data </returns>
        /// <exception cref="IOException"> If an I/O error occurs </exception>
        public abstract SegmentInfo Read(Directory directory, string segmentName, IOContext context);
    }
}