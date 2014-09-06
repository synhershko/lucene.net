using System;

namespace Lucene.Net.Analysis.Tokenattributes
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

    using Attribute = Lucene.Net.Util.Attribute;

    /// <summary>
    /// Default implementation of <seealso cref="OffsetAttribute"/>. </summary>
    public class OffsetAttribute : Attribute, IOffsetAttribute, ICloneable
    {
        private int StartOffset_Renamed;
        private int EndOffset_Renamed;

        /// <summary>
        /// Initialize this attribute with startOffset and endOffset of 0. </summary>
        public OffsetAttribute()
        {
        }

        public int StartOffset()
        {
            return StartOffset_Renamed;
        }

        public void SetOffset(int startOffset, int endOffset)
        {
            // TODO: we could assert that this is set-once, ie,
            // current values are -1?  Very few token filters should
            // change offsets once set by the tokenizer... and
            // tokenizer should call clearAtts before re-using
            // OffsetAtt

            if (startOffset < 0 || endOffset < startOffset)
            {
                throw new System.ArgumentException("startOffset must be non-negative, and endOffset must be >= startOffset, " + "startOffset=" + startOffset + ",endOffset=" + endOffset);
            }

            this.StartOffset_Renamed = startOffset;
            this.EndOffset_Renamed = endOffset;
        }

        public int EndOffset()
        {
            return EndOffset_Renamed;
        }

        public override void Clear()
        {
            // TODO: we could use -1 as default here?  Then we can
            // assert in setOffset...
            StartOffset_Renamed = 0;
            EndOffset_Renamed = 0;
        }

        public override bool Equals(object other)
        {
            if (other == this)
            {
                return true;
            }

            if (other is OffsetAttribute)
            {
                OffsetAttribute o = (OffsetAttribute)other;
                return o.StartOffset_Renamed == StartOffset_Renamed && o.EndOffset_Renamed == EndOffset_Renamed;
            }

            return false;
        }

        public override int GetHashCode()
        {
            int code = StartOffset_Renamed;
            code = code * 31 + EndOffset_Renamed;
            return code;
        }

        public override void CopyTo(Attribute target)
        {
            OffsetAttribute t = (OffsetAttribute)target;
            t.SetOffset(StartOffset_Renamed, EndOffset_Renamed);
        }
    }
}