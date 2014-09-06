using System.Collections.Generic;
using System.Diagnostics;

namespace Lucene.Net.Index
{
    using System;

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

    using Directory = Lucene.Net.Store.Directory;

    /// <summary>
    /// An <seealso cref="IndexDeletionPolicy"/> that wraps any other
    /// <seealso cref="IndexDeletionPolicy"/> and adds the ability to hold and later release
    /// snapshots of an index. While a snapshot is held, the <seealso cref="IndexWriter"/> will
    /// not remove any files associated with it even if the index is otherwise being
    /// actively, arbitrarily changed. Because we wrap another arbitrary
    /// <seealso cref="IndexDeletionPolicy"/>, this gives you the freedom to continue using
    /// whatever <seealso cref="IndexDeletionPolicy"/> you would normally want to use with your
    /// index.
    ///
    /// <p>
    /// this class maintains all snapshots in-memory, and so the information is not
    /// persisted and not protected against system failures. If persistence is
    /// important, you can use <seealso cref="PersistentSnapshotDeletionPolicy"/>.
    ///
    /// @lucene.experimental
    /// </summary>
    public class SnapshotDeletionPolicy : IndexDeletionPolicy
    {
        /// <summary>
        /// Records how many snapshots are held against each
        ///  commit generation
        /// </summary>
        protected internal IDictionary<long, int> RefCounts = new Dictionary<long, int>();

        /// <summary>
        /// Used to map gen to IndexCommit. </summary>
        protected internal IDictionary<long?, IndexCommit> IndexCommits = new Dictionary<long?, IndexCommit>();

        /// <summary>
        /// Wrapped <seealso cref="IndexDeletionPolicy"/> </summary>
        private IndexDeletionPolicy Primary;

        /// <summary>
        /// Most recently committed <seealso cref="IndexCommit"/>. </summary>
        protected internal IndexCommit LastCommit;

        /// <summary>
        /// Used to detect misuse </summary>
        private bool InitCalled;

        /// <summary>
        /// Sole constructor, taking the incoming {@link
        ///  IndexDeletionPolicy} to wrap.
        /// </summary>
        public SnapshotDeletionPolicy(IndexDeletionPolicy primary)
        {
            this.Primary = primary;
        }

        public override void OnCommit<T>(IList<T> commits)
        {
            lock (this)
            {
                Primary.OnCommit(WrapCommits(commits));
                LastCommit = commits[commits.Count - 1];
            }
        }

        public override void OnInit<T>(IList<T> commits)
        {
            lock (this)
            {
                InitCalled = true;
                Primary.OnInit(WrapCommits(commits));
                foreach (IndexCommit commit in commits)
                {
                    if (RefCounts.ContainsKey(commit.Generation))
                    {
                        IndexCommits[commit.Generation] = commit;
                    }
                }
                if (commits.Count > 0)
                {
                    LastCommit = commits[commits.Count - 1];
                }
            }
        }

        /// <summary>
        /// Release a snapshotted commit.
        /// </summary>
        /// <param name="commit">
        ///          the commit previously returned by <seealso cref="#snapshot"/> </param>
        public virtual void Release(IndexCommit commit)
        {
            lock (this)
            {
                long gen = commit.Generation;
                ReleaseGen(gen);
            }
        }

        /// <summary>
        /// Release a snapshot by generation. </summary>
        protected internal virtual void ReleaseGen(long gen)
        {
            if (!InitCalled)
            {
                throw new InvalidOperationException("this instance is not being used by IndexWriter; be sure to use the instance returned from writer.getConfig().getIndexDeletionPolicy()");
            }
            int? refCount = RefCounts[gen];
            if (refCount == null)
            {
                throw new System.ArgumentException("commit gen=" + gen + " is not currently snapshotted");
            }
            int refCountInt = (int)refCount;
            Debug.Assert(refCountInt > 0);
            refCountInt--;
            if (refCountInt == 0)
            {
                RefCounts.Remove(gen);
                IndexCommits.Remove(gen);
            }
            else
            {
                RefCounts[gen] = refCountInt;
            }
        }

        /// <summary>
        /// Increments the refCount for this <seealso cref="IndexCommit"/>. </summary>
        protected internal virtual void IncRef(IndexCommit ic)
        {
            lock (this)
            {
                long gen = ic.Generation;
                int refCount;
                int refCountInt;
                if (!RefCounts.TryGetValue(gen, out refCount))
                {
                    IndexCommits[gen] = LastCommit;
                    refCountInt = 0;
                }
                else
                {
                    refCountInt = (int)refCount;
                }
                RefCounts[gen] = refCountInt + 1;
            }
        }

        /// <summary>
        /// Snapshots the last commit and returns it. Once a commit is 'snapshotted,' it is protected
        /// from deletion (as long as this <seealso cref="IndexDeletionPolicy"/> is used). The
        /// snapshot can be removed by calling <seealso cref="#release(IndexCommit)"/> followed
        /// by a call to <seealso cref="IndexWriter#deleteUnusedFiles()"/>.
        ///
        /// <p>
        /// <b>NOTE:</b> while the snapshot is held, the files it references will not
        /// be deleted, which will consume additional disk space in your index. If you
        /// take a snapshot at a particularly bad time (say just before you call
        /// forceMerge) then in the worst case this could consume an extra 1X of your
        /// total index size, until you release the snapshot.
        /// </summary>
        /// <exception cref="IllegalStateException">
        ///           if this index does not have any commits yet </exception>
        /// <returns> the <seealso cref="IndexCommit"/> that was snapshotted. </returns>
        public virtual IndexCommit Snapshot()
        {
            lock (this)
            {
                if (!InitCalled)
                {
                    throw new InvalidOperationException("this instance is not being used by IndexWriter; be sure to use the instance returned from writer.getConfig().getIndexDeletionPolicy()");
                }
                if (LastCommit == null)
                {
                    // No commit yet, eg this is a new IndexWriter:
                    throw new InvalidOperationException("No index commit to snapshot");
                }

                IncRef(LastCommit);

                return LastCommit;
            }
        }

        /// <summary>
        /// Returns all IndexCommits held by at least one snapshot. </summary>
        public virtual IList<IndexCommit> Snapshots
        {
            get
            {
                lock (this)
                {
                    return new List<IndexCommit>(IndexCommits.Values);
                }
            }
        }

        /// <summary>
        /// Returns the total number of snapshots currently held. </summary>
        public virtual int SnapshotCount
        {
            get
            {
                lock (this)
                {
                    int total = 0;
                    foreach (int? refCount in RefCounts.Values)
                    {
                        total += (int)refCount;
                    }

                    return total;
                }
            }
        }

        /// <summary>
        /// Retrieve an <seealso cref="IndexCommit"/> from its generation;
        ///  returns null if this IndexCommit is not currently
        ///  snapshotted
        /// </summary>
        public virtual IndexCommit GetIndexCommit(long gen)
        {
            lock (this)
            {
                return IndexCommits[gen];
            }
        }

        public object Clone()
        {
            lock (this)
            {
                SnapshotDeletionPolicy other = (SnapshotDeletionPolicy)base.Clone();
                other.Primary = (IndexDeletionPolicy)this.Primary.Clone();
                other.LastCommit = null;
                other.RefCounts = new Dictionary<long, int>(RefCounts);
                other.IndexCommits = new Dictionary<long?, IndexCommit>(IndexCommits);
                return other;
            }
        }

        /// <summary>
        /// Wraps each <seealso cref="IndexCommit"/> as a {@link
        ///  SnapshotCommitPoint}.
        /// </summary>
        private IList<IndexCommit> WrapCommits<T>(IList<T> commits)
            where T : IndexCommit
        {
            IList<IndexCommit> wrappedCommits = new List<IndexCommit>(commits.Count);
            foreach (IndexCommit ic in commits)
            {
                wrappedCommits.Add(new SnapshotCommitPoint(this, ic));
            }
            return wrappedCommits;
        }

        /// <summary>
        /// Wraps a provided <seealso cref="IndexCommit"/> and prevents it
        ///  from being deleted.
        /// </summary>
        private class SnapshotCommitPoint : IndexCommit
        {
            private readonly SnapshotDeletionPolicy OuterInstance;

            /// <summary>
            /// The <seealso cref="IndexCommit"/> we are preventing from deletion. </summary>
            protected internal IndexCommit Cp;

            /// <summary>
            /// Creates a {@code SnapshotCommitPoint} wrapping the provided
            ///  <seealso cref="IndexCommit"/>.
            /// </summary>
            protected internal SnapshotCommitPoint(SnapshotDeletionPolicy outerInstance, IndexCommit cp)
            {
                this.OuterInstance = outerInstance;
                this.Cp = cp;
            }

            public override string ToString()
            {
                return "SnapshotDeletionPolicy.SnapshotCommitPoint(" + Cp + ")";
            }

            public override void Delete()
            {
                lock (OuterInstance)
                {
                    // Suppress the delete request if this commit point is
                    // currently snapshotted.
                    if (!OuterInstance.RefCounts.ContainsKey(Cp.Generation))
                    {
                        Cp.Delete();
                    }
                }
            }

            public override Directory Directory
            {
                get
                {
                    return Cp.Directory;
                }
            }

            public override ICollection<string> FileNames
            {
                get
                {
                    return Cp.FileNames;
                }
            }

            public override long Generation
            {
                get
                {
                    return Cp.Generation;
                }
            }

            public override string SegmentsFileName
            {
                get
                {
                    return Cp.SegmentsFileName;
                }
            }

            public override IDictionary<string, string> UserData
            {
                get
                {
                    return Cp.UserData;
                }
            }

            public override bool Deleted
            {
                get
                {
                    return Cp.Deleted;
                }
            }

            public override int SegmentCount
            {
                get
                {
                    return Cp.SegmentCount;
                }
            }
        }
    }
}