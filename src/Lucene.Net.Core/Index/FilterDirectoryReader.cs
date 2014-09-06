using System.Collections.Generic;
using System.Linq;

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
    /// A FilterDirectoryReader wraps another DirectoryReader, allowing implementations
    /// to transform or extend it.
    ///
    /// Subclasses should implement doWrapDirectoryReader to return an instance of the
    /// subclass.
    ///
    /// If the subclass wants to wrap the DirectoryReader's subreaders, it should also
    /// implement a SubReaderWrapper subclass, and pass an instance to its super
    /// constructor.
    /// </summary>
    public abstract class FilterDirectoryReader : DirectoryReader
    {
        /// <summary>
        /// Factory class passed to FilterDirectoryReader constructor that allows
        /// subclasses to wrap the filtered DirectoryReader's subreaders.  You
        /// can use this to, e.g., wrap the subreaders with specialised
        /// FilterAtomicReader implementations.
        /// </summary>
        public abstract class SubReaderWrapper
        {
            internal virtual AtomicReader[] Wrap(IList<AtomicReader> readers)
            {
                AtomicReader[] wrapped = new AtomicReader[readers.Count];
                for (int i = 0; i < readers.Count; i++)
                {
                    wrapped[i] = Wrap(readers[i]);
                }
                return wrapped;
            }

            /// <summary>
            /// Constructor </summary>
            public SubReaderWrapper()
            {
            }

            /// <summary>
            /// Wrap one of the parent DirectoryReader's subreaders </summary>
            /// <param name="reader"> the subreader to wrap </param>
            /// <returns> a wrapped/filtered AtomicReader </returns>
            public abstract AtomicReader Wrap(AtomicReader reader);
        }

        /// <summary>
        /// A no-op SubReaderWrapper that simply returns the parent
        /// DirectoryReader's original subreaders.
        /// </summary>
        public class StandardReaderWrapper : SubReaderWrapper
        {
            /// <summary>
            /// Constructor </summary>
            public StandardReaderWrapper()
            {
            }

            public override AtomicReader Wrap(AtomicReader reader)
            {
                return reader;
            }
        }

        /// <summary>
        /// The filtered DirectoryReader </summary>
        protected internal readonly DirectoryReader @in;

        /// <summary>
        /// Create a new FilterDirectoryReader that filters a passed in DirectoryReader. </summary>
        /// <param name="in"> the DirectoryReader to filter </param>
        public FilterDirectoryReader(DirectoryReader @in)
            : this(@in, new StandardReaderWrapper())
        {
        }

        /// <summary>
        /// Create a new FilterDirectoryReader that filters a passed in DirectoryReader,
        /// using the supplied SubReaderWrapper to wrap its subreader. </summary>
        /// <param name="in"> the DirectoryReader to filter </param>
        /// <param name="wrapper"> the SubReaderWrapper to use to wrap subreaders </param>
        public FilterDirectoryReader(DirectoryReader @in, SubReaderWrapper wrapper)
            : base(@in.Directory(), wrapper.Wrap(@in.GetSequentialSubReaders().OfType<AtomicReader>().ToList()))
        {
            this.@in = @in;
        }

        /// <summary>
        /// Called by the doOpenIfChanged() methods to return a new wrapped DirectoryReader.
        ///
        /// Implementations should just return an instantiation of themselves, wrapping the
        /// passed in DirectoryReader.
        /// </summary>
        /// <param name="in"> the DirectoryReader to wrap </param>
        /// <returns> the wrapped DirectoryReader </returns>
        protected internal abstract DirectoryReader DoWrapDirectoryReader(DirectoryReader @in);

        private DirectoryReader WrapDirectoryReader(DirectoryReader @in)
        {
            return @in == null ? null : DoWrapDirectoryReader(@in);
        }

        protected internal override sealed DirectoryReader DoOpenIfChanged()
        {
            return WrapDirectoryReader(@in.DoOpenIfChanged());
        }

        protected internal override sealed DirectoryReader DoOpenIfChanged(IndexCommit commit)
        {
            return WrapDirectoryReader(@in.DoOpenIfChanged(commit));
        }

        protected internal override sealed DirectoryReader DoOpenIfChanged(IndexWriter writer, bool applyAllDeletes)
        {
            return WrapDirectoryReader(@in.DoOpenIfChanged(writer, applyAllDeletes));
        }

        public override long Version
        {
            get
            {
                return @in.Version;
            }
        }

        public override bool Current
        {
            get
            {
                return @in.Current;
            }
        }

        public override IndexCommit IndexCommit
        {
            get
            {
                return @in.IndexCommit;
            }
        }

        protected internal override void DoClose()
        {
            @in.DoClose();
        }
    }
}