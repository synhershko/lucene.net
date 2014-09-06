namespace Lucene.Net.Analysis.Tokenattributes
{
    using Lucene.Net.Util;

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
    using BytesRef = Lucene.Net.Util.BytesRef;

    /// <summary>
    /// The payload of a Token.
    /// <p>
    /// The payload is stored in the index at each position, and can
    /// be used to influence scoring when using Payload-based queries
    /// in the <seealso cref="Lucene.Net.Search.Payloads"/> and
    /// <seealso cref="Lucene.Net.Search.Spans"/> packages.
    /// <p>
    /// NOTE: because the payload will be stored at each position, its usually
    /// best to use the minimum number of bytes necessary. Some codec implementations
    /// may optimize payload storage when all payloads have the same length.
    /// </summary>
    /// <seealso cref= DocsAndPositionsEnum </seealso>
    public interface IPayloadAttribute : IAttribute
    {
        /// <summary>
        /// Returns this Token's payload. </summary>
        /// <seealso cref= #setPayload(BytesRef) </seealso>
        BytesRef Payload { get; set; }
    }
}