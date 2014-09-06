using System.Collections.Generic;
using System.Text;

namespace Lucene.Net.Search.Spans
{
    using Lucene.Net.Support;

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

    using ToStringUtils = Lucene.Net.Util.ToStringUtils;

    /// <summary>
    /// Only return those matches that have a specific payload at
    /// the given position.
    /// <p/>
    ///
    /// </summary>
    public class SpanNearPayloadCheckQuery : SpanPositionCheckQuery
    {
        protected internal readonly ICollection<sbyte[]> PayloadToMatch;

        /// <param name="match">          The underlying <seealso cref="SpanQuery"/> to check </param>
        /// <param name="payloadToMatch"> The <seealso cref="java.util.Collection"/> of payloads to match </param>
        public SpanNearPayloadCheckQuery(SpanNearQuery match, ICollection<sbyte[]> payloadToMatch)
            : base(match)
        {
            this.PayloadToMatch = payloadToMatch;
        }

        protected internal override AcceptStatus AcceptPosition(Spans spans)
        {
            bool result = spans.PayloadAvailable;
            if (result == true)
            {
                ICollection<sbyte[]> candidate = spans.Payload;
                if (candidate.Count == PayloadToMatch.Count)
                {
                    //TODO: check the byte arrays are the same
                    //hmm, can't rely on order here
                    int matches = 0;
                    foreach (sbyte[] candBytes in candidate)
                    {
                        //Unfortunately, we can't rely on order, so we need to compare all
                        foreach (sbyte[] payBytes in PayloadToMatch)
                        {
                            if (Arrays.Equals(candBytes, payBytes) == true)
                            {
                                matches++;
                                break;
                            }
                        }
                    }
                    if (matches == PayloadToMatch.Count)
                    {
                        //we've verified all the bytes
                        return AcceptStatus.YES;
                    }
                    else
                    {
                        return AcceptStatus.NO;
                    }
                }
                else
                {
                    return AcceptStatus.NO;
                }
            }
            return AcceptStatus.NO;
        }

        public override string ToString(string field)
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("spanPayCheck(");
            buffer.Append(match.ToString(field));
            buffer.Append(", payloadRef: ");
            foreach (sbyte[] bytes in PayloadToMatch)
            {
                ToStringUtils.ByteArray(buffer, bytes);
                buffer.Append(';');
            }
            buffer.Append(")");
            buffer.Append(ToStringUtils.Boost(Boost));
            return buffer.ToString();
        }

        public override object Clone()
        {
            SpanNearPayloadCheckQuery result = new SpanNearPayloadCheckQuery((SpanNearQuery)match.Clone(), PayloadToMatch);
            result.Boost = Boost;
            return result;
        }

        public override bool Equals(object o)
        {
            if (this == o)
            {
                return true;
            }
            if (!(o is SpanNearPayloadCheckQuery))
            {
                return false;
            }

            SpanNearPayloadCheckQuery other = (SpanNearPayloadCheckQuery)o;
            return this.PayloadToMatch.Equals(other.PayloadToMatch) && this.match.Equals(other.match) && this.Boost == other.Boost;
        }

        public override int GetHashCode()
        {
            int h = match.GetHashCode();
            h ^= (h << 8) | ((int)((uint)h >> 25)); // reversible
            //TODO: is this right?
            h ^= PayloadToMatch.GetHashCode();
            h ^= Number.FloatToIntBits(Boost);
            return h;
        }
    }
}