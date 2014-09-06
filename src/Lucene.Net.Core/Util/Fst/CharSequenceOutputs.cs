using System;
using System.Diagnostics;

namespace Lucene.Net.Util.Fst
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

    using DataInput = Lucene.Net.Store.DataInput;
    using DataOutput = Lucene.Net.Store.DataOutput;

    /// <summary>
    /// An FST <seealso cref="Outputs"/> implementation where each output
    /// is a sequence of characters.
    ///
    /// @lucene.experimental
    /// </summary>

    public sealed class CharSequenceOutputs : Outputs<CharsRef>
    {
        private static readonly CharsRef NO_OUTPUT = new CharsRef();
        private static readonly CharSequenceOutputs Singleton_Renamed = new CharSequenceOutputs();

        private CharSequenceOutputs()
        {
        }

        public static CharSequenceOutputs Singleton
        {
            get
            {
                return Singleton_Renamed;
            }
        }

        public override CharsRef Common(CharsRef output1, CharsRef output2)
        {
            Debug.Assert(output1 != null);
            Debug.Assert(output2 != null);

            int pos1 = output1.Offset;
            int pos2 = output2.Offset;
            int stopAt1 = pos1 + Math.Min(output1.length, output2.length);
            while (pos1 < stopAt1)
            {
                if (output1.Chars[pos1] != output2.Chars[pos2])
                {
                    break;
                }
                pos1++;
                pos2++;
            }

            if (pos1 == output1.Offset)
            {
                // no common prefix
                return NO_OUTPUT;
            }
            else if (pos1 == output1.Offset + output1.length)
            {
                // output1 is a prefix of output2
                return output1;
            }
            else if (pos2 == output2.Offset + output2.length)
            {
                // output2 is a prefix of output1
                return output2;
            }
            else
            {
                return new CharsRef(output1.Chars, output1.Offset, pos1 - output1.Offset);
            }
        }

        public override CharsRef Subtract(CharsRef output, CharsRef inc)
        {
            Debug.Assert(output != null);
            Debug.Assert(inc != null);
            if (inc == NO_OUTPUT)
            {
                // no prefix removed
                return output;
            }
            else if (inc.length == output.length)
            {
                // entire output removed
                return NO_OUTPUT;
            }
            else
            {
                Debug.Assert(inc.length < output.length, "inc.length=" + inc.length + " vs output.length=" + output.length);
                Debug.Assert(inc.length > 0);
                return new CharsRef(output.Chars, output.Offset + inc.length, output.length - inc.length);
            }
        }

        public override CharsRef Add(CharsRef prefix, CharsRef output)
        {
            Debug.Assert(prefix != null);
            Debug.Assert(output != null);
            if (prefix == NO_OUTPUT)
            {
                return output;
            }
            else if (output == NO_OUTPUT)
            {
                return prefix;
            }
            else
            {
                Debug.Assert(prefix.length > 0);
                Debug.Assert(output.length > 0);
                var result = new CharsRef(prefix.length + output.length);
                Array.Copy(prefix.Chars, prefix.Offset, result.Chars, 0, prefix.length);
                Array.Copy(output.Chars, output.Offset, result.Chars, prefix.length, output.length);
                result.length = prefix.length + output.length;
                return result;
            }
        }

        public override void Write(CharsRef prefix, DataOutput @out)
        {
            Debug.Assert(prefix != null);
            @out.WriteVInt(prefix.length);
            // TODO: maybe UTF8?
            for (int idx = 0; idx < prefix.length; idx++)
            {
                @out.WriteVInt(prefix.Chars[prefix.Offset + idx]);
            }
        }

        public override CharsRef Read(DataInput @in)
        {
            int len = @in.ReadVInt();
            if (len == 0)
            {
                return NO_OUTPUT;
            }
            else
            {
                CharsRef output = new CharsRef(len);
                for (int idx = 0; idx < len; idx++)
                {
                    output.Chars[idx] = (char)@in.ReadVInt();
                }
                output.length = len;
                return output;
            }
        }

        public override CharsRef NoOutput
        {
            get
            {
                return NO_OUTPUT;
            }
        }

        public override string OutputToString(CharsRef output)
        {
            return output.ToString();
        }
    }
}