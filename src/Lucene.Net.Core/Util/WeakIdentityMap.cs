using Lucene.Net.Support;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lucene.Net.Util
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
    /// Implements a combination of <seealso cref="java.util.WeakHashMap"/> and
    /// <seealso cref="java.util.IdentityHashMap"/>.
    /// Useful for caches that need to key off of a {@code ==} comparison
    /// instead of a {@code .equals}.
    ///
    /// <p>this class is not a general-purpose <seealso cref="java.util.Map"/>
    /// implementation! It intentionally violates
    /// Map's general contract, which mandates the use of the equals method
    /// when comparing objects. this class is designed for use only in the
    /// rare cases wherein reference-equality semantics are required.
    ///
    /// <p>this implementation was forked from <a href="http://cxf.apache.org/">Apache CXF</a>
    /// but modified to <b>not</b> implement the <seealso cref="java.util.Map"/> interface and
    /// without any set views on it, as those are error-prone and inefficient,
    /// if not implemented carefully. The map only contains <seealso cref="Iterator"/> implementations
    /// on the values and not-GCed keys. Lucene's implementation also supports {@code null}
    /// keys, but those are never weak!
    ///
    /// <p><a name="reapInfo" />The map supports two modes of operation:
    /// <ul>
    ///  <li>{@code reapOnRead = true}: this behaves identical to a <seealso cref="java.util.WeakHashMap"/>
    ///  where it also cleans up the reference queue on every read operation (<seealso cref="#get(Object)"/>,
    ///  <seealso cref="#containsKey(Object)"/>, <seealso cref="#size()"/>, <seealso cref="#valueIterator()"/>), freeing map entries
    ///  of already GCed keys.</li>
    ///  <li>{@code reapOnRead = false}: this mode does not call <seealso cref="#reap()"/> on every read
    ///  operation. In this case, the reference queue is only cleaned up on write operations
    ///  (like <seealso cref="#put(Object, Object)"/>). this is ideal for maps with few entries where
    ///  the keys are unlikely be garbage collected, but there are lots of <seealso cref="#get(Object)"/>
    ///  operations. The code can still call <seealso cref="#reap()"/> to manually clean up the queue without
    ///  doing a write operation.</li>
    /// </ul>
    ///
    /// @lucene.internal
    /// </summary>
    public sealed class WeakIdentityMap<K, V>
        where K : class
    {
        //private readonly ReferenceQueue<object> queue = new ReferenceQueue<object>();
        private readonly IDictionary<IdentityWeakReference, V> BackingStore;

        private readonly bool ReapOnRead;

        /// <summary>
        /// Creates a new {@code WeakIdentityMap} based on a non-synchronized <seealso cref="HashMap"/>.
        /// The map <a href="#reapInfo">cleans up the reference queue on every read operation</a>.
        /// </summary>
        public static WeakIdentityMap<K, V> newHashMap()
        {
            return NewHashMap(false);
        }

        /// <summary>
        /// Creates a new {@code WeakIdentityMap} based on a non-synchronized <seealso cref="HashMap"/>. </summary>
        /// <param name="reapOnRead"> controls if the map <a href="#reapInfo">cleans up the reference queue on every read operation</a>. </param>
        public static WeakIdentityMap<K, V> NewHashMap(bool reapOnRead)
        {
            return new WeakIdentityMap<K, V>(new HashMap<IdentityWeakReference, V>(), reapOnRead);
        }

        /// <summary>
        /// Creates a new {@code WeakIdentityMap} based on a <seealso cref="ConcurrentHashMap"/>.
        /// The map <a href="#reapInfo">cleans up the reference queue on every read operation</a>.
        /// </summary>
        public static WeakIdentityMap<K, V> NewConcurrentHashMap()
        {
            return NewConcurrentHashMap(true);
        }

        /// <summary>
        /// Creates a new {@code WeakIdentityMap} based on a <seealso cref="ConcurrentHashMap"/>. </summary>
        /// <param name="reapOnRead"> controls if the map <a href="#reapInfo">cleans up the reference queue on every read operation</a>. </param>
        public static WeakIdentityMap<K, V> NewConcurrentHashMap(bool reapOnRead)
        {
            return new WeakIdentityMap<K, V>(new ConcurrentDictionary<IdentityWeakReference, V>(), reapOnRead);
        }

        /// <summary>
        /// Private only constructor, to create use the static factory methods. </summary>
        private WeakIdentityMap(IDictionary<IdentityWeakReference, V> backingStore, bool reapOnRead)
        {
            this.BackingStore = backingStore;
            this.ReapOnRead = reapOnRead;
        }

        /// <summary>
        /// Removes all of the mappings from this map. </summary>
        public void Clear()
        {
            BackingStore.Clear();
            Reap();
        }

        /// <summary>
        /// Returns {@code true} if this map contains a mapping for the specified key. </summary>
        public bool ContainsKey(object key)
        {
            if (ReapOnRead)
            {
                Reap();
            }
            return BackingStore.ContainsKey(new IdentityWeakReference(key));
        }

        /// <summary>
        /// Returns the value to which the specified key is mapped. </summary>
        public V Get(object key)
        {
            if (ReapOnRead)
            {
                Reap();
            }
            return BackingStore[new IdentityWeakReference(key)];
        }

        /// <summary>
        /// Associates the specified value with the specified key in this map.
        /// If the map previously contained a mapping for this key, the old value
        /// is replaced.
        /// </summary>
        public V Put(K key, V value)
        {
            Reap();
            return BackingStore[new IdentityWeakReference(key)] = value;
        }

        public IEnumerable<K> Keys
        {
            // .NET port: using this method which mimics IDictionary instead of KeyIterator()
            get
            {
                foreach (var key in BackingStore.Keys)
                {
                    var target = key.Target;

                    if (target == null)
                        continue;
                    else if (target == NULL)
                        yield return null;
                    else
                        yield return (K)target;
                }
            }
        }

        public IEnumerable<V> Values
        {
            get
            {
                if (ReapOnRead) Reap();
                return BackingStore.Values;
            }
        }

        /// <summary>
        /// Returns {@code true} if this map contains no key-value mappings. </summary>
        public bool Empty
        {
            get
            {
                return Size() == 0;
            }
        }

        /// <summary>
        /// Removes the mapping for a key from this weak hash map if it is present.
        /// Returns the value to which this map previously associated the key,
        /// or {@code null} if the map contained no mapping for the key.
        /// A return value of {@code null} does not necessarily indicate that
        /// the map contained.
        /// </summary>
        public bool Remove(object key)
        {
            Reap();
            return BackingStore.Remove(new IdentityWeakReference(key));
        }

        /// <summary>
        /// Returns the number of key-value mappings in this map. this result is a snapshot,
        /// and may not reflect unprocessed entries that will be removed before next
        /// attempted access because they are no longer referenced.
        /// </summary>
        public int Size()
        {
            if (BackingStore.Count == 0)
            {
                return 0;
            }
            if (ReapOnRead)
            {
                Reap();
            }
            return BackingStore.Count;
        }

        /*LUCENE TO-DO I don't think necessary
        /// <summary>
        /// Returns an iterator over all weak keys of this map.
        /// Keys already garbage collected will not be returned.
        /// this Iterator does not support removals.
        /// </summary>
        public IEnumerator<K> KeyIterator()
        {
          Reap();
          IEnumerator<IdentityWeakReference> iterator = BackingStore.Keys.GetEnumerator();
          // IMPORTANT: Don't use oal.util.FilterIterator here:
          // We need *strong* reference to current key after setNext()!!!
          return new IteratorAnonymousInnerClassHelper(this, iterator);
        }

        private class IteratorAnonymousInnerClassHelper : Iterator<K>
        {
            private readonly WeakIdentityMap<K,V> OuterInstance;

            private IEnumerator<IdentityWeakReference> Iterator;

            public IteratorAnonymousInnerClassHelper(WeakIdentityMap<K,V> outerInstance, IEnumerator<IdentityWeakReference> iterator)
            {
                this.OuterInstance = outerInstance;
                this.Iterator = iterator;
                next = null;
                nextIsSet = false;
            }

              // holds strong reference to next element in backing iterator:
            private object next;
            // the backing iterator was already consumed:
            private bool nextIsSet;
            /
            public virtual bool HasNext()
            {
              return nextIsSet || SetNext();
            }

            public virtual K Next()
            {
              if (!HasNext())
              {
                throw new Exception();
              }
              Debug.Assert(nextIsSet);
              try
              {
                return (K) next;
              }
              finally
              {
                 // release strong reference and invalidate current value:
                nextIsSet = false;
                next = null;
              }
            }

            public virtual void Remove()
            {
              throw new System.NotSupportedException();
            }

            private bool SetNext()
            {
              Debug.Assert(!nextIsSet);
              while (Iterator.MoveNext())
              {
                next = Iterator.Current;
                if (next == null)
                {
                  // the key was already GCed, we can remove it from backing map:
                  Iterator.remove();
                }
                else
                {
                  // unfold "null" special value:
                  if (next == NULL)
                  {
                    next = null;
                  }
                  return nextIsSet = true;
                }
              }
              return false;
            }
        }*/

        /// <summary>
        /// Returns an iterator over all values of this map.
        /// this iterator may return values whose key is already
        /// garbage collected while iterator is consumed,
        /// especially if {@code reapOnRead} is {@code false}.
        /// </summary>
        public IEnumerator<V> ValueIterator()
        {
            if (ReapOnRead)
            {
                Reap();
            }
            return BackingStore.Values.GetEnumerator();
        }

        /// <summary>
        /// this method manually cleans up the reference queue to remove all garbage
        /// collected key/value pairs from the map. Calling this method is not needed
        /// if {@code reapOnRead = true}. Otherwise it might be a good idea
        /// to call this method when there is spare time (e.g. from a background thread). </summary>
        /// <seealso cref= <a href="#reapInfo">Information about the <code>reapOnRead</code> setting</a> </seealso>
        public void Reap()
        {
            lock (BackingStore)
            {
                List<IdentityWeakReference> keysToRemove = new List<IdentityWeakReference>();

                foreach (IdentityWeakReference zombie in BackingStore.Keys)
                {
                    if (!zombie.IsAlive)
                        keysToRemove.Add(zombie);
                }

                foreach (var key in keysToRemove)
                {
                    BackingStore.Remove(key);
                }
            }
            /*while ((zombie = queue.poll()) != null)
            {
              BackingStore.Remove(zombie);
            }*/
        }

        // we keep a hard reference to our NULL key, so map supports null keys that never get GCed:
        internal static readonly object NULL = new object();

        private sealed class IdentityWeakReference : WeakReference
        {
            internal readonly int Hash;

            internal IdentityWeakReference(object obj/*, ReferenceQueue<object> queue*/)
                : base(obj == null ? NULL : obj/*, queue*/)
            {
                Hash = RuntimeHelpers.GetHashCode(obj);
            }

            public override int GetHashCode()
            {
                return Hash;
            }

            public override bool Equals(object o)
            {
                if (this == o)
                {
                    return true;
                }
                if (o is IdentityWeakReference)
                {
                    IdentityWeakReference @ref = (IdentityWeakReference)o;
                    if (this.Target == @ref.Target)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}