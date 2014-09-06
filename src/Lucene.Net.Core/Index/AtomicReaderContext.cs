using System.Collections.Generic;
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

    /// <summary>
    /// <seealso cref="IndexReaderContext"/> for <seealso cref="AtomicReader"/> instances.
    /// </summary>
    public sealed class AtomicReaderContext : IndexReaderContext
    {
        /// <summary>
        /// The readers ord in the top-level's leaves array </summary>
        public readonly int Ord;

        /// <summary>
        /// The readers absolute doc base </summary>
        public readonly int DocBase;

        private readonly AtomicReader reader;
        private readonly IList<AtomicReaderContext> leaves;

        /// <summary>
        /// Creates a new <seealso cref="AtomicReaderContext"/>
        /// </summary>
        internal AtomicReaderContext(CompositeReaderContext parent, AtomicReader reader, int ord, int docBase, int leafOrd, int leafDocBase)
            : base(parent, ord, docBase)
        {
            this.Ord = leafOrd;
            this.DocBase = leafDocBase;
            this.reader = reader;
            this.leaves = IsTopLevel ? new[] { this } : null; //LUCENE TO-DO suspicous
        }

        internal AtomicReaderContext(AtomicReader atomicReader)
            : this(null, atomicReader, 0, 0, 0, 0)
        {
        }

        public override IList<AtomicReaderContext> Leaves()
        {
            if (!IsTopLevel)
            {
                throw new System.NotSupportedException("this is not a top-level context.");
            }
            Debug.Assert(leaves != null);
            return leaves;
        }

        public override IList<IndexReaderContext> Children()
        {
            return null;
        }

        public override IndexReader Reader()
        {
            return reader;
        }

        // .NET Port: Can't change return type on override like Java, so adding helper property
        // to avoid a bunch of casting.
        public AtomicReader AtomicReader
        {
            get
            {
                return reader;
            }
        }
    }
}