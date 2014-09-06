using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Lucene.Net.Index
{
    using Lucene.Net.Support;
    using Lucene.Net.Util;
    using Bits = Lucene.Net.Util.Bits;

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

    using Codec = Lucene.Net.Codecs.Codec;
    using CompoundFileDirectory = Lucene.Net.Store.CompoundFileDirectory;
    using Directory = Lucene.Net.Store.Directory;
    using DocValuesFormat = Lucene.Net.Codecs.DocValuesFormat;
    using DocValuesProducer = Lucene.Net.Codecs.DocValuesProducer;
    using DocValuesType = Lucene.Net.Index.FieldInfo.DocValuesType_e;
    using FieldCache = Lucene.Net.Search.FieldCache;
    using IOContext = Lucene.Net.Store.IOContext;
    using IOUtils = Lucene.Net.Util.IOUtils;
    using StoredFieldsReader = Lucene.Net.Codecs.StoredFieldsReader;
    using TermVectorsReader = Lucene.Net.Codecs.TermVectorsReader;

    /// <summary>
    /// IndexReader implementation over a single segment.
    /// <p>
    /// Instances pointing to the same segment (but with different deletes, etc)
    /// may share the same core data.
    /// @lucene.experimental
    /// </summary>
    public sealed class SegmentReader : AtomicReader
    {
        private readonly SegmentCommitInfo Si;
        private readonly Bits LiveDocs_Renamed;

        // Normally set to si.docCount - si.delDocCount, unless we
        // were created as an NRT reader from IW, in which case IW
        // tells us the docCount:
        private readonly int NumDocs_Renamed;

        internal readonly SegmentCoreReaders Core;
        internal readonly SegmentDocValues SegDocValues;

        internal readonly IDisposableThreadLocal<IDictionary<string, object>> docValuesLocal = new IDisposableThreadLocalAnonymousInnerClassHelper();

        private class IDisposableThreadLocalAnonymousInnerClassHelper : IDisposableThreadLocal<IDictionary<string, object>>
        {
            public IDisposableThreadLocalAnonymousInnerClassHelper()
            {
            }

            protected internal override IDictionary<string, object> InitialValue()
            {
                return new Dictionary<string, object>();
            }
        }

        internal readonly IDisposableThreadLocal<IDictionary<string, Bits>> docsWithFieldLocal = new IDisposableThreadLocalAnonymousInnerClassHelper2();

        private class IDisposableThreadLocalAnonymousInnerClassHelper2 : IDisposableThreadLocal<IDictionary<string, Bits>>
        {
            public IDisposableThreadLocalAnonymousInnerClassHelper2()
            {
            }

            protected internal override IDictionary<string, Bits> InitialValue()
            {
                return new Dictionary<string, Bits>();
            }
        }

        internal readonly IDictionary<string, DocValuesProducer> DvProducersByField = new Dictionary<string, DocValuesProducer>();
        internal readonly ISet<DocValuesProducer> DvProducers = new IdentityHashSet<DocValuesProducer>();

        internal readonly FieldInfos FieldInfos_Renamed;

        private readonly IList<long?> DvGens = new List<long?>();

        /// <summary>
        /// Constructs a new SegmentReader with a new core. </summary>
        /// <exception cref="CorruptIndexException"> if the index is corrupt </exception>
        /// <exception cref="IOException"> if there is a low-level IO error </exception>
        // TODO: why is this public?
        public SegmentReader(SegmentCommitInfo si, int termInfosIndexDivisor, IOContext context)
        {
            this.Si = si;
            // TODO if the segment uses CFS, we may open the CFS file twice: once for
            // reading the FieldInfos (if they are not gen'd) and second time by
            // SegmentCoreReaders. We can open the CFS here and pass to SCR, but then it
            // results in less readable code (resource not closed where it was opened).
            // Best if we could somehow read FieldInfos in SCR but not keep it there, but
            // constructors don't allow returning two things...
            FieldInfos_Renamed = ReadFieldInfos(si);
            Core = new SegmentCoreReaders(this, si.Info.Dir, si, context, termInfosIndexDivisor);
            SegDocValues = new SegmentDocValues();

            bool success = false;
            Codec codec = si.Info.Codec;
            try
            {
                if (si.HasDeletions())
                {
                    // NOTE: the bitvector is stored using the regular directory, not cfs
                    LiveDocs_Renamed = codec.LiveDocsFormat().ReadLiveDocs(Directory(), si, IOContext.READONCE);
                }
                else
                {
                    Debug.Assert(si.DelCount == 0);
                    LiveDocs_Renamed = null;
                }
                NumDocs_Renamed = si.Info.DocCount - si.DelCount;

                if (FieldInfos_Renamed.HasDocValues())
                {
                    InitDocValuesProducers(codec);
                }

                success = true;
            }
            finally
            {
                // With lock-less commits, it's entirely possible (and
                // fine) to hit a FileNotFound exception above.  In
                // this case, we want to explicitly close any subset
                // of things that were opened so that we don't have to
                // wait for a GC to do so.
                if (!success)
                {
                    DoClose();
                }
            }
        }

        /// <summary>
        /// Create new SegmentReader sharing core from a previous
        ///  SegmentReader and loading new live docs from a new
        ///  deletes file.  Used by openIfChanged.
        /// </summary>
        internal SegmentReader(SegmentCommitInfo si, SegmentReader sr)
            : this(si, sr, si.Info.Codec.LiveDocsFormat().ReadLiveDocs(si.Info.Dir, si, IOContext.READONCE), si.Info.DocCount - si.DelCount)
        {
        }

        /// <summary>
        /// Create new SegmentReader sharing core from a previous
        ///  SegmentReader and using the provided in-memory
        ///  liveDocs.  Used by IndexWriter to provide a new NRT
        ///  reader
        /// </summary>
        internal SegmentReader(SegmentCommitInfo si, SegmentReader sr, Bits liveDocs, int numDocs)
        {
            this.Si = si;
            this.LiveDocs_Renamed = liveDocs;
            this.NumDocs_Renamed = numDocs;
            this.Core = sr.Core;
            Core.IncRef();
            this.SegDocValues = sr.SegDocValues;

            //    System.out.println("[" + Thread.currentThread().getName() + "] SR.init: sharing reader: " + sr + " for gens=" + sr.genDVProducers.keySet());

            // increment refCount of DocValuesProducers that are used by this reader
            bool success = false;
            try
            {
                Codec codec = si.Info.Codec;
                if (si.FieldInfosGen == -1)
                {
                    FieldInfos_Renamed = sr.FieldInfos_Renamed;
                }
                else
                {
                    FieldInfos_Renamed = ReadFieldInfos(si);
                }

                if (FieldInfos_Renamed.HasDocValues())
                {
                    InitDocValuesProducers(codec);
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    DoClose();
                }
            }
        }

        // initialize the per-field DocValuesProducer
        private void InitDocValuesProducers(Codec codec)
        {
            Directory dir = Core.CfsReader != null ? Core.CfsReader : Si.Info.Dir;
            DocValuesFormat dvFormat = codec.DocValuesFormat();
            IDictionary<long?, IList<FieldInfo>> genInfos = GenInfos;

            //      System.out.println("[" + Thread.currentThread().getName() + "] SR.initDocValuesProducers: segInfo=" + si + "; gens=" + genInfos.keySet());

            // TODO: can we avoid iterating over fieldinfos several times and creating maps of all this stuff if dv updates do not exist?

            foreach (KeyValuePair<long?, IList<FieldInfo>> e in genInfos)
            {
                long? gen = e.Key;
                IList<FieldInfo> infos = e.Value;
                DocValuesProducer dvp = SegDocValues.GetDocValuesProducer(gen, Si, IOContext.READ, dir, dvFormat, infos, TermInfosIndexDivisor);
                foreach (FieldInfo fi in infos)
                {
                    DvProducersByField[fi.Name] = dvp;
                    DvProducers.Add(dvp);
                }
            }

            DvGens.AddRange(genInfos.Keys);
        }

        /// <summary>
        /// Reads the most recent <seealso cref="FieldInfos"/> of the given segment info.
        ///
        /// @lucene.internal
        /// </summary>
        public static FieldInfos ReadFieldInfos(SegmentCommitInfo info)
        {
            Directory dir;
            bool closeDir;
            if (info.FieldInfosGen == -1 && info.Info.UseCompoundFile)
            {
                // no fieldInfos gen and segment uses a compound file
                dir = new CompoundFileDirectory(info.Info.Dir, IndexFileNames.SegmentFileName(info.Info.Name, "", IndexFileNames.COMPOUND_FILE_EXTENSION), IOContext.READONCE, false);
                closeDir = true;
            }
            else
            {
                // gen'd FIS are read outside CFS, or the segment doesn't use a compound file
                dir = info.Info.Dir;
                closeDir = false;
            }

            try
            {
                string segmentSuffix = info.FieldInfosGen == -1 ? "" : info.FieldInfosGen.ToString(CultureInfo.InvariantCulture);//Convert.ToString(info.FieldInfosGen, Character.MAX_RADIX));
                return info.Info.Codec.FieldInfosFormat().FieldInfosReader.Read(dir, info.Info.Name, segmentSuffix, IOContext.READONCE);
            }
            finally
            {
                if (closeDir)
                {
                    dir.Dispose();
                }
            }
        }

        // returns a gen->List<FieldInfo> mapping. Fields without DV updates have gen=-1
        private IDictionary<long?, IList<FieldInfo>> GenInfos
        {
            get
            {
                IDictionary<long?, IList<FieldInfo>> genInfos = new Dictionary<long?, IList<FieldInfo>>();
                foreach (FieldInfo fi in FieldInfos_Renamed)
                {
                    if (fi.DocValuesType == null)
                    {
                        continue;
                    }
                    long gen = fi.DocValuesGen;
                    IList<FieldInfo> infos;
                    genInfos.TryGetValue(gen, out infos);
                    if (infos == null)
                    {
                        infos = new List<FieldInfo>();
                        genInfos[gen] = infos;
                    }
                    infos.Add(fi);
                }
                return genInfos;
            }
        }

        public override Bits LiveDocs
        {
            get
            {
                EnsureOpen();
                return LiveDocs_Renamed;
            }
        }

        protected internal override void DoClose()
        {
            //System.out.println("SR.close seg=" + si);
            try
            {
                Core.DecRef();
            }
            finally
            {
                DvProducersByField.Clear();
                try
                {
                    IOUtils.Close(docValuesLocal, docsWithFieldLocal);
                }
                finally
                {
                    SegDocValues.DecRef(DvGens);
                }
            }
        }

        public override FieldInfos FieldInfos
        {
            get
            {
                EnsureOpen();
                return FieldInfos_Renamed;
            }
        }

        /// <summary>
        /// Expert: retrieve thread-private {@link
        ///  StoredFieldsReader}
        ///  @lucene.internal
        /// </summary>
        public StoredFieldsReader FieldsReader
        {
            get
            {
                EnsureOpen();
                return Core.fieldsReaderLocal.Get();
            }
        }

        public override void Document(int docID, StoredFieldVisitor visitor)
        {
            CheckBounds(docID);
            FieldsReader.VisitDocument(docID, visitor);
        }

        public override Fields Fields()
        {
            EnsureOpen();
            return Core.Fields;
        }

        public override int NumDocs()
        {
            // Don't call ensureOpen() here (it could affect performance)
            return NumDocs_Renamed;
        }

        public override int MaxDoc()
        {
            // Don't call ensureOpen() here (it could affect performance)
            return Si.Info.DocCount;
        }

        /// <summary>
        /// Expert: retrieve thread-private {@link
        ///  TermVectorsReader}
        ///  @lucene.internal
        /// </summary>
        public TermVectorsReader TermVectorsReader
        {
            get
            {
                EnsureOpen();
                return Core.termVectorsLocal.Get();
            }
        }

        public override Fields GetTermVectors(int docID)
        {
            TermVectorsReader termVectorsReader = TermVectorsReader;
            if (termVectorsReader == null)
            {
                return null;
            }
            CheckBounds(docID);
            return termVectorsReader.Get(docID);
        }

        private void CheckBounds(int docID)
        {
            if (docID < 0 || docID >= MaxDoc())
            {
                throw new System.IndexOutOfRangeException("docID must be >= 0 and < maxDoc=" + MaxDoc() + " (got docID=" + docID + ")");
            }
        }

        public override string ToString()
        {
            // SegmentInfo.toString takes dir and number of
            // *pending* deletions; so we reverse compute that here:
            return Si.ToString(Si.Info.Dir, Si.Info.DocCount - NumDocs_Renamed - Si.DelCount);
        }

        /// <summary>
        /// Return the name of the segment this reader is reading.
        /// </summary>
        public string SegmentName
        {
            get
            {
                return Si.Info.Name;
            }
        }

        /// <summary>
        /// Return the SegmentInfoPerCommit of the segment this reader is reading.
        /// </summary>
        public SegmentCommitInfo SegmentInfo
        {
            get
            {
                return Si;
            }
        }

        /// <summary>
        /// Returns the directory this index resides in. </summary>
        public Directory Directory()
        {
            // Don't ensureOpen here -- in certain cases, when a
            // cloned/reopened reader needs to commit, it may call
            // this method on the closed original reader
            return Si.Info.Dir;
        }

        // this is necessary so that cloned SegmentReaders (which
        // share the underlying postings data) will map to the
        // same entry in the FieldCache.  See LUCENE-1579.
        public override object CoreCacheKey
        {
            get
            {
                // NOTE: if this ever changes, be sure to fix
                // SegmentCoreReader.notifyCoreClosedListeners to match!
                // Today it passes "this" as its coreCacheKey:
                return Core;
            }
        }

        public override object CombinedCoreAndDeletesKey
        {
            get
            {
                return this;
            }
        }

        /// <summary>
        /// Returns term infos index divisor originally passed to
        ///  <seealso cref="#SegmentReader(SegmentCommitInfo, int, IOContext)"/>.
        /// </summary>
        public int TermInfosIndexDivisor
        {
            get
            {
                return Core.TermsIndexDivisor;
            }
        }

        // returns the FieldInfo that corresponds to the given field and type, or
        // null if the field does not exist, or not indexed as the requested
        // DovDocValuesType.
        private FieldInfo GetDVField(string field, DocValuesType type)
        {
            FieldInfo fi = FieldInfos_Renamed.FieldInfo(field);
            if (fi == null)
            {
                // Field does not exist
                return null;
            }
            if (fi.DocValuesType == null)
            {
                // Field was not indexed with doc values
                return null;
            }
            if (fi.DocValuesType != type)
            {
                // Field DocValues are different than requested type
                return null;
            }

            return fi;
        }

        public override NumericDocValues GetNumericDocValues(string field)
        {
            EnsureOpen();
            FieldInfo fi = GetDVField(field, DocValuesType.NUMERIC);
            if (fi == null)
            {
                return null;
            }

            IDictionary<string, object> dvFields = docValuesLocal.Get();

            NumericDocValues dvs;
            object dvsDummy;
            dvFields.TryGetValue(field, out dvsDummy);
            dvs = (NumericDocValues)dvsDummy;
            if (dvs == null)
            {
                DocValuesProducer dvProducer;
                DvProducersByField.TryGetValue(field, out dvProducer);
                Debug.Assert(dvProducer != null);
                dvs = dvProducer.GetNumeric(fi);
                dvFields[field] = dvs;
            }

            return dvs;
        }

        public override Bits GetDocsWithField(string field)
        {
            EnsureOpen();
            FieldInfo fi = FieldInfos_Renamed.FieldInfo(field);
            if (fi == null)
            {
                // Field does not exist
                return null;
            }
            if (fi.DocValuesType == null)
            {
                // Field was not indexed with doc values
                return null;
            }

            IDictionary<string, Bits> dvFields = docsWithFieldLocal.Get();

            Bits dvs;
            dvFields.TryGetValue(field, out dvs);
            if (dvs == null)
            {
                DocValuesProducer dvProducer;
                DvProducersByField.TryGetValue(field, out dvProducer);
                Debug.Assert(dvProducer != null);
                dvs = dvProducer.GetDocsWithField(fi);
                dvFields[field] = dvs;
            }

            return dvs;
        }

        public override BinaryDocValues GetBinaryDocValues(string field)
        {
            EnsureOpen();
            FieldInfo fi = GetDVField(field, DocValuesType.BINARY);
            if (fi == null)
            {
                return null;
            }

            IDictionary<string, object> dvFields = docValuesLocal.Get();

            object ret;
            BinaryDocValues dvs;
            dvFields.TryGetValue(field, out ret);
            dvs = (BinaryDocValues)ret;
            if (dvs == null)
            {
                DocValuesProducer dvProducer;
                DvProducersByField.TryGetValue(field, out dvProducer);
                Debug.Assert(dvProducer != null);
                dvs = dvProducer.GetBinary(fi);
                dvFields[field] = dvs;
            }

            return dvs;
        }

        public override SortedDocValues GetSortedDocValues(string field)
        {
            EnsureOpen();
            FieldInfo fi = GetDVField(field, DocValuesType.SORTED);
            if (fi == null)
            {
                return null;
            }

            IDictionary<string, object> dvFields = docValuesLocal.Get();

            SortedDocValues dvs;
            object ret;
            dvFields.TryGetValue(field, out ret);
            dvs = (SortedDocValues)ret;
            if (dvs == null)
            {
                DocValuesProducer dvProducer;
                DvProducersByField.TryGetValue(field, out dvProducer);
                Debug.Assert(dvProducer != null);
                dvs = dvProducer.GetSorted(fi);
                dvFields[field] = dvs;
            }

            return dvs;
        }

        public override SortedSetDocValues GetSortedSetDocValues(string field)
        {
            EnsureOpen();
            FieldInfo fi = GetDVField(field, DocValuesType.SORTED_SET);
            if (fi == null)
            {
                return null;
            }

            IDictionary<string, object> dvFields = docValuesLocal.Get();

            object ret;
            SortedSetDocValues dvs;
            dvFields.TryGetValue(field, out ret);
            dvs = (SortedSetDocValues)ret;
            if (dvs == null)
            {
                DocValuesProducer dvProducer;
                DvProducersByField.TryGetValue(field, out dvProducer);
                Debug.Assert(dvProducer != null);
                dvs = dvProducer.GetSortedSet(fi);
                dvFields[field] = dvs;
            }

            return dvs;
        }

        public override NumericDocValues GetNormValues(string field)
        {
            EnsureOpen();
            FieldInfo fi = FieldInfos_Renamed.FieldInfo(field);
            if (fi == null || !fi.HasNorms())
            {
                // Field does not exist or does not index norms
                return null;
            }
            return Core.GetNormValues(fi);
        }

        /// <summary>
        /// Called when the shared core for this SegmentReader
        /// is closed.
        /// <p>
        /// this listener is called only once all SegmentReaders
        /// sharing the same core are closed.  At this point it
        /// is safe for apps to evict this reader from any caches
        /// keyed on <seealso cref="#getCoreCacheKey"/>.  this is the same
        /// interface that <seealso cref="FieldCache"/> uses, internally,
        /// to evict entries.</p>
        ///
        /// @lucene.experimental
        /// </summary>
        public interface CoreClosedListener
        {
            /// <summary>
            /// Invoked when the shared core of the original {@code
            ///  SegmentReader} has closed.
            /// </summary>
            void OnClose(object ownerCoreCacheKey);
        }

        /// <summary>
        /// Expert: adds a CoreClosedListener to this reader's shared core </summary>
        public void AddCoreClosedListener(CoreClosedListener listener)
        {
            EnsureOpen();
            Core.AddCoreClosedListener(listener);
        }

        /// <summary>
        /// Expert: removes a CoreClosedListener from this reader's shared core </summary>
        public void RemoveCoreClosedListener(CoreClosedListener listener)
        {
            EnsureOpen();
            Core.RemoveCoreClosedListener(listener);
        }

        /// <summary>
        /// Returns approximate RAM Bytes used </summary>
        public long RamBytesUsed()
        {
            EnsureOpen();
            long ramBytesUsed = 0;
            if (DvProducers != null)
            {
                foreach (DocValuesProducer producer in DvProducers)
                {
                    ramBytesUsed += producer.RamBytesUsed();
                }
            }
            if (Core != null)
            {
                ramBytesUsed += Core.RamBytesUsed();
            }
            return ramBytesUsed;
        }

        public override void CheckIntegrity()
        {
            EnsureOpen();

            // stored fields
            FieldsReader.CheckIntegrity();

            // term vectors
            TermVectorsReader termVectorsReader = TermVectorsReader;
            if (termVectorsReader != null)
            {
                termVectorsReader.CheckIntegrity();
            }

            // terms/postings
            if (Core.Fields != null)
            {
                Core.Fields.CheckIntegrity();
            }

            // norms
            if (Core.NormsProducer != null)
            {
                Core.NormsProducer.CheckIntegrity();
            }

            // docvalues
            if (DvProducers != null)
            {
                foreach (DocValuesProducer producer in DvProducers)
                {
                    producer.CheckIntegrity();
                }
            }
        }
    }
}