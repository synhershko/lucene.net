using System.Threading;

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

    using ThreadInterruptedException = Lucene.Net.Util.ThreadInterruptedException;

    /// <summary>
    /// hangs onto files a little bit longer (50ms in close).
    /// MockDirectoryWrapper acts like windows: you can't delete files
    /// open elsewhere. so the idea is to make race conditions for tiny
    /// files (like segments) easier to reproduce.
    /// </summary>
    internal class SlowClosingMockIndexInputWrapper : MockIndexInputWrapper
    {
        public SlowClosingMockIndexInputWrapper(MockDirectoryWrapper dir, string name, IndexInput @delegate)
            : base(dir, name, @delegate)
        {
        }

        public override void Dispose()
        {
            try
            {
                Thread.Sleep(50);
            }
            catch (ThreadInterruptedException ie)
            {
                throw new ThreadInterruptedException(ie);
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}