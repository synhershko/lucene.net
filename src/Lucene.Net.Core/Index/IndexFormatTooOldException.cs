using System.Diagnostics;

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

namespace Lucene.Net.Index
{
    using DataInput = Lucene.Net.Store.DataInput;

    /// <summary>
    /// this exception is thrown when Lucene detects
    /// an index that is too old for this Lucene version
    /// </summary>
    public class IndexFormatTooOldException : CorruptIndexException
    {
        /// <summary>
        /// Creates an {@code IndexFormatTooOldException}.
        /// </summary>
        ///  <param name="resourceDesc"> describes the file that was too old </param>
        ///  <param name="version"> the version of the file that was too old
        ///
        /// @lucene.internal  </param>
        public IndexFormatTooOldException(string resourceDesc, string version)
            : base("Format version is not supported (resource: " + resourceDesc + "): " + version + ". this version of Lucene only supports indexes created with release 3.0 and later.")
        {
            Debug.Assert(resourceDesc != null);
        }

        /// <summary>
        /// Creates an {@code IndexFormatTooOldException}.
        /// </summary>
        ///  <param name="in"> the open file that's too old </param>
        ///  <param name="version"> the version of the file that was too old
        ///
        /// @lucene.internal  </param>
        public IndexFormatTooOldException(DataInput @in, string version)
            : this(@in.ToString(), version)
        {
        }

        /// <summary>
        /// Creates an {@code IndexFormatTooOldException}.
        /// </summary>
        ///  <param name="resourceDesc"> describes the file that was too old </param>
        ///  <param name="version"> the version of the file that was too old </param>
        ///  <param name="minVersion"> the minimum version accepted </param>
        ///  <param name="maxVersion"> the maxium version accepted
        ///
        /// @lucene.internal  </param>
        public IndexFormatTooOldException(string resourceDesc, int version, int minVersion, int maxVersion)
            : base("Format version is not supported (resource: " + resourceDesc + "): " + version + " (needs to be between " + minVersion + " and " + maxVersion + "). this version of Lucene only supports indexes created with release 3.0 and later.")
        {
            Debug.Assert(resourceDesc != null);
        }

        /// <summary>
        /// Creates an {@code IndexFormatTooOldException}.
        /// </summary>
        ///  <param name="in"> the open file that's too old </param>
        ///  <param name="version"> the version of the file that was too old </param>
        ///  <param name="minVersion"> the minimum version accepted </param>
        ///  <param name="maxVersion"> the maxium version accepted
        ///
        /// @lucene.internal  </param>
        public IndexFormatTooOldException(DataInput @in, int version, int minVersion, int maxVersion)
            : this(@in.ToString(), version, minVersion, maxVersion)
        {
        }
    }
}