using System.Diagnostics;

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

    using BytesRef = Lucene.Net.Util.BytesRef;

    /// <summary>
    /// Exposes multi-valued view over a single-valued instance.
    /// <p>
    /// this can be used if you want to have one multi-valued implementation
    /// against e.g. FieldCache.getDocTermOrds that also works for single-valued
    /// fields.
    /// </summary>
    public sealed class SingletonSortedSetDocValues : SortedSetDocValues
    {
        private readonly SortedDocValues @in;
        private int DocID;
        private bool Set;

        /// <summary>
        /// Creates a multi-valued view over the provided SortedDocValues </summary>
        public SingletonSortedSetDocValues(SortedDocValues @in)
        {
            this.@in = @in;
            Debug.Assert(NO_MORE_ORDS == -1); // this allows our nextOrd() to work for missing values without a check
        }

        /// <summary>
        /// Return the wrapped <seealso cref="SortedDocValues"/> </summary>
        public SortedDocValues SortedDocValues
        {
            get
            {
                return @in;
            }
        }

        public override long NextOrd()
        {
            if (Set)
            {
                return NO_MORE_ORDS;
            }
            else
            {
                Set = true;
                return @in.GetOrd(DocID);
            }
        }

        public override int Document
        {
            set
            {
                this.DocID = value;
                Set = false;
            }
        }

        public override void LookupOrd(long ord, BytesRef result)
        {
            // cast is ok: single-valued cannot exceed Integer.MAX_VALUE
            @in.LookupOrd((int)ord, result);
        }

        public override long ValueCount
        {
            get
            {
                return @in.ValueCount;
            }
        }

        public override long LookupTerm(BytesRef key)
        {
            return @in.LookupTerm(key);
        }
    }
}