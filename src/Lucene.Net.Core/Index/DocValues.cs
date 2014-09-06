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

    using Bits = Lucene.Net.Util.Bits;
    using BytesRef = Lucene.Net.Util.BytesRef;

    /// <summary>
    /// this class contains utility methods and constants for DocValues
    /// </summary>
    public sealed class DocValues
    {
        /* no instantiation */

        private DocValues()
        {
        }

        /// <summary>
        /// An empty BinaryDocValues which returns <seealso cref="BytesRef#EMPTY_BYTES"/> for every document
        /// </summary>
        public static readonly BinaryDocValues EMPTY_BINARY = new BinaryDocValuesAnonymousInnerClassHelper();

        private class BinaryDocValuesAnonymousInnerClassHelper : BinaryDocValues
        {
            public BinaryDocValuesAnonymousInnerClassHelper()
            {
            }

            public override void Get(int docID, BytesRef result)
            {
                result.Bytes = BytesRef.EMPTY_BYTES;
                result.Offset = 0;
                result.Length = 0;
            }
        }

        /// <summary>
        /// An empty NumericDocValues which returns zero for every document
        /// </summary>
        public static readonly NumericDocValues EMPTY_NUMERIC = new NumericDocValuesAnonymousInnerClassHelper();

        private class NumericDocValuesAnonymousInnerClassHelper : NumericDocValues
        {
            public NumericDocValuesAnonymousInnerClassHelper()
            {
            }

            public override long Get(int docID)
            {
                return 0;
            }
        }

        /// <summary>
        /// An empty SortedDocValues which returns <seealso cref="BytesRef#EMPTY_BYTES"/> for every document
        /// </summary>
        public static readonly SortedDocValues EMPTY_SORTED = new SortedDocValuesAnonymousInnerClassHelper();

        private class SortedDocValuesAnonymousInnerClassHelper : SortedDocValues
        {
            public SortedDocValuesAnonymousInnerClassHelper()
            {
            }

            public override int GetOrd(int docID)
            {
                return -1;
            }

            public override void LookupOrd(int ord, BytesRef result)
            {
                result.Bytes = BytesRef.EMPTY_BYTES;
                result.Offset = 0;
                result.Length = 0;
            }

            public override int ValueCount
            {
                get
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// An empty SortedDocValues which returns <seealso cref="SortedSetDocValues#NO_MORE_ORDS"/> for every document
        /// </summary>
        public static readonly SortedSetDocValues EMPTY_SORTED_SET = new RandomAccessOrdsAnonymousInnerClassHelper();

        private class RandomAccessOrdsAnonymousInnerClassHelper : RandomAccessOrds
        {
            public RandomAccessOrdsAnonymousInnerClassHelper()
            {
            }

            public override long NextOrd()
            {
                return NO_MORE_ORDS;
            }

            public override int Document
            {
                set
                {
                }
            }

            public override void LookupOrd(long ord, BytesRef result)
            {
                throw new System.IndexOutOfRangeException();
            }

            public override long ValueCount
            {
                get
                {
                    return 0;
                }
            }

            public override long OrdAt(int index)
            {
                throw new System.IndexOutOfRangeException();
            }

            public override int Cardinality()
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns a multi-valued view over the provided SortedDocValues
        /// </summary>
        public static SortedSetDocValues Singleton(SortedDocValues dv)
        {
            return new SingletonSortedSetDocValues(dv);
        }

        /// <summary>
        /// Returns a single-valued view of the SortedSetDocValues, if it was previously
        /// wrapped with <seealso cref="#singleton"/>, or null.
        /// </summary>
        public static SortedDocValues UnwrapSingleton(SortedSetDocValues dv)
        {
            if (dv is SingletonSortedSetDocValues)
            {
                return ((SingletonSortedSetDocValues)dv).SortedDocValues;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a Bits representing all documents from <code>dv</code> that have a value.
        /// </summary>
        public static Bits DocsWithValue(SortedDocValues dv, int maxDoc)
        {
            return new BitsAnonymousInnerClassHelper(dv, maxDoc);
        }

        private class BitsAnonymousInnerClassHelper : Bits
        {
            private Lucene.Net.Index.SortedDocValues Dv;
            private int MaxDoc;

            public BitsAnonymousInnerClassHelper(Lucene.Net.Index.SortedDocValues dv, int maxDoc)
            {
                this.Dv = dv;
                this.MaxDoc = maxDoc;
            }

            public virtual bool Get(int index)
            {
                return Dv.GetOrd(index) >= 0;
            }

            public virtual int Length()
            {
                return MaxDoc;
            }
        }

        /// <summary>
        /// Returns a Bits representing all documents from <code>dv</code> that have a value.
        /// </summary>
        public static Bits DocsWithValue(SortedSetDocValues dv, int maxDoc)
        {
            return new BitsAnonymousInnerClassHelper2(dv, maxDoc);
        }

        private class BitsAnonymousInnerClassHelper2 : Bits
        {
            private Lucene.Net.Index.SortedSetDocValues Dv;
            private int MaxDoc;

            public BitsAnonymousInnerClassHelper2(Lucene.Net.Index.SortedSetDocValues dv, int maxDoc)
            {
                this.Dv = dv;
                this.MaxDoc = maxDoc;
            }

            public virtual bool Get(int index)
            {
                Dv.Document = index;
                return Dv.NextOrd() != SortedSetDocValues.NO_MORE_ORDS;
            }

            public virtual int Length()
            {
                return MaxDoc;
            }
        }
    }
}