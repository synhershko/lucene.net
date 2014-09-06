using System.Diagnostics;

namespace Lucene.Net.Codecs
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

    using DocsEnum = Lucene.Net.Index.DocsEnum; // javadocs
    using OrdTermState = Lucene.Net.Index.OrdTermState;
    using TermState = Lucene.Net.Index.TermState;

    /// <summary>
    /// Holds all state required for <seealso cref="PostingsReaderBase"/>
    /// to produce a <seealso cref="DocsEnum"/> without re-seeking the
    /// terms dict.
    /// </summary>
    public class BlockTermState : OrdTermState
    {
        /// <summary>
        /// how many docs have this term </summary>
        public int DocFreq;

        /// <summary>
        /// total number of occurrences of this term </summary>
        public long TotalTermFreq;

        /// <summary>
        /// the term's ord in the current block </summary>
        public int TermBlockOrd;

        /// <summary>
        /// fp into the terms dict primary file (_X.tim) that holds this term </summary>
        // TODO: update BTR to nuke this
        public long BlockFilePointer;

        /// <summary>
        /// Sole constructor. (For invocation by subclass
        ///  constructors, typically implicit.)
        /// </summary>
        protected internal BlockTermState()
        {
        }

        public override void CopyFrom(TermState _other)
        {
            Debug.Assert(_other is BlockTermState, "can not copy from " + _other.GetType().Name);
            BlockTermState other = (BlockTermState)_other;
            base.CopyFrom(_other);
            DocFreq = other.DocFreq;
            TotalTermFreq = other.TotalTermFreq;
            TermBlockOrd = other.TermBlockOrd;
            BlockFilePointer = other.BlockFilePointer;
        }

        public override string ToString()
        {
            return "docFreq=" + DocFreq + " totalTermFreq=" + TotalTermFreq + " termBlockOrd=" + TermBlockOrd + " blockFP=" + BlockFilePointer;
        }
    }
}