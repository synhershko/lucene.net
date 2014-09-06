using System;

namespace Lucene.Net.Codecs
{
    /// <summary>
    /// Copyright 2004 The Apache Software Foundation
    ///
    /// Licensed under the Apache License, Version 2.0 (the "License"); you may not
    /// use this file except in compliance with the License. You may obtain a copy of
    /// the License at
    ///
    /// http://www.apache.org/licenses/LICENSE-2.0
    ///
    /// Unless required by applicable law or agreed to in writing, software
    /// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
    /// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
    /// License for the specific language governing permissions and limitations under
    /// the License.
    /// </summary>

    using StoredFieldVisitor = Lucene.Net.Index.StoredFieldVisitor;

    /// <summary>
    /// Codec API for reading stored fields.
    /// <p>
    /// You need to implement <seealso cref="#visitDocument(int, StoredFieldVisitor)"/> to
    /// read the stored fields for a document, implement <seealso cref="#clone()"/> (creating
    /// clones of any IndexInputs used, etc), and <seealso cref="#close()"/>
    /// @lucene.experimental
    /// </summary>
    public abstract class StoredFieldsReader : ICloneable, IDisposable
    {
        /// <summary>
        /// Sole constructor. (For invocation by subclass
        ///  constructors, typically implicit.)
        /// </summary>
        protected internal StoredFieldsReader()
        {
        }

        /// <summary>
        /// Visit the stored fields for document <code>n</code> </summary>
        public abstract void VisitDocument(int n, StoredFieldVisitor visitor);

        public abstract object Clone();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        /// <summary>
        /// Returns approximate RAM bytes used </summary>
        public abstract long RamBytesUsed();

        /// <summary>
        /// Checks consistency of this reader.
        /// <p>
        /// Note that this may be costly in terms of I/O, e.g.
        /// may involve computing a checksum value against large data files.
        /// @lucene.internal
        /// </summary>
        public abstract void CheckIntegrity();
    }
}