namespace Lucene.Net.Index
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

    /// <summary>
    /// Implement this class to plug into the TermsHash
    ///  processor, which inverts & stores Tokens into a hash
    ///  table and provides an API for writing bytes into
    ///  multiple streams for each unique Token.
    /// </summary>
    public abstract class TermsHashConsumerPerField
    {
        internal abstract bool Start(IndexableField[] fields, int count);

        internal abstract void Finish();

        internal abstract void SkippingLongTerm();

        internal abstract void Start(IndexableField field);

        internal abstract void NewTerm(int termID);

        internal abstract void AddTerm(int termID);

        internal abstract int StreamCount { get; }

        internal abstract ParallelPostingsArray CreatePostingsArray(int size);
    }
}