using System;

namespace Lucene.Net.Search
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
    /// Throw this exception in <seealso cref="Collector#collect(int)"/> to prematurely
    ///  terminate collection of the current leaf.
    ///  <p>Note: IndexSearcher swallows this exception and never re-throws it.
    ///  As a consequence, you should not catch it when calling
    ///  <seealso cref="IndexSearcher#search"/> as it is unnecessary and might hide misuse
    ///  of this exception.
    /// </summary>
    //JAVA TO C# CONVERTER TODO TASK: Most Java annotations will not have direct .NET equivalent attributes:
    //ORIGINAL LINE: @SuppressWarnings("serial") public final class CollectionTerminatedException extends RuntimeException
    public sealed class CollectionTerminatedException : Exception
    {
        /// <summary>
        /// Sole constructor. </summary>
        public CollectionTerminatedException()
            : base()
        {
        }
    }
}