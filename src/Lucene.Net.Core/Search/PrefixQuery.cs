using System.Text;

namespace Lucene.Net.Search
{
    using AttributeSource = Lucene.Net.Util.AttributeSource;
    using Term = Lucene.Net.Index.Term;
    using Terms = Lucene.Net.Index.Terms;

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

    using TermsEnum = Lucene.Net.Index.TermsEnum;
    using ToStringUtils = Lucene.Net.Util.ToStringUtils;

    /// <summary>
    /// A Query that matches documents containing terms with a specified prefix. A PrefixQuery
    /// is built by QueryParser for input like <code>app*</code>.
    ///
    /// <p>this query uses the {@link
    /// MultiTermQuery#CONSTANT_SCORE_AUTO_REWRITE_DEFAULT}
    /// rewrite method.
    /// </summary>
    public class PrefixQuery : MultiTermQuery
    {
        private Term Prefix_Renamed;

        /// <summary>
        /// Constructs a query for terms starting with <code>prefix</code>. </summary>
        public PrefixQuery(Term prefix)
            : base(prefix.Field())
        {
            this.Prefix_Renamed = prefix;
        }

        /// <summary>
        /// Returns the prefix of this query. </summary>
        public virtual Term Prefix
        {
            get
            {
                return Prefix_Renamed;
            }
        }

        public override TermsEnum GetTermsEnum(Terms terms, AttributeSource atts)
        {
            TermsEnum tenum = terms.Iterator(null);

            if (Prefix_Renamed.Bytes().Length == 0)
            {
                // no prefix -- match all terms for this field:
                return tenum;
            }
            return new PrefixTermsEnum(tenum, Prefix_Renamed.Bytes());
        }

        /// <summary>
        /// Prints a user-readable version of this query. </summary>
        public override string ToString(string field)
        {
            StringBuilder buffer = new StringBuilder();
            if (!Field.Equals(field))
            {
                buffer.Append(Field);
                buffer.Append(":");
            }
            buffer.Append(Prefix_Renamed.Text());
            buffer.Append('*');
            buffer.Append(ToStringUtils.Boost(Boost));
            return buffer.ToString();
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = base.GetHashCode();
            result = prime * result + ((Prefix_Renamed == null) ? 0 : Prefix_Renamed.GetHashCode());
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (!base.Equals(obj))
            {
                return false;
            }
            if (this.GetType() != obj.GetType())
            {
                return false;
            }
            PrefixQuery other = (PrefixQuery)obj;
            if (Prefix_Renamed == null)
            {
                if (other.Prefix_Renamed != null)
                {
                    return false;
                }
            }
            else if (!Prefix_Renamed.Equals(other.Prefix_Renamed))
            {
                return false;
            }
            return true;
        }
    }
}