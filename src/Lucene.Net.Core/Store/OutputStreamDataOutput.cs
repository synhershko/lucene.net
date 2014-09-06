using System;
using System.IO;

namespace Lucene.Net.Store
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
    /// A <seealso cref="DataOutput"/> wrapping a plain <seealso cref="OutputStream"/>.
    /// </summary>
    public class OutputStreamDataOutput : DataOutput, IDisposable
    {
        private readonly Stream Os;

        public OutputStreamDataOutput(Stream os)
        {
            this.Os = os;
        }

        public override void WriteByte(byte b)
        {
            Os.WriteByte(unchecked((byte)b));
        }

        public override void WriteBytes(byte[] b, int offset, int length)
        {
            Os.Write(b, offset, length);
        }

        public void Dispose()
        {
            Os.Close();
        }
    }
}