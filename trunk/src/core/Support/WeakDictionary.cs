using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Lucene.Net.Support
{
    public class WeakDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private HashMap<WeakKey<TKey>, TValue> _hm;
        private int _gcCollections = 0;
        private int _changes = 0;

        public WeakDictionary(int initialCapacity)
        { }

        public WeakDictionary() : this(32, Enumerable.Empty<KeyValuePair<TKey, TValue>>())
        { }

        public WeakDictionary(IEnumerable<KeyValuePair<TKey, TValue>> otherDictionary) : this(32, otherDictionary)
        { }

        private WeakDictionary(int initialCapacity, IEnumerable<KeyValuePair<TKey, TValue>> otherDict)
        {
            _hm = new HashMap<WeakKey<TKey>, TValue>(initialCapacity);
            foreach (var kvp in otherDict)
            {
                _hm.Add(new WeakKey<TKey>(kvp.Key), kvp.Value);
            }
        }

        private void Clean()
        {
            while (!_hm.Keys.Where(x => x != null).Any(x => !x.IsAlive))
            {
                _hm.Remove(_hm.Keys.Where(x => !x.IsAlive).First());
            }
        }

        private void CleanIfNeeded()
        {
            _gcCollections = GC.CollectionCount(0);
            if (_gcCollections > 0)
            {
                Clean();
                _gcCollections = 0;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var kvp in Enumerable.Where(_hm, x => x.Key.IsAlive))
            {
                yield return new KeyValuePair<TKey, TValue>(kvp.Key.Target, kvp.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            CleanIfNeeded();
            ((ICollection<KeyValuePair<TKey, TValue>>)_hm).Add(item);
        }

        public void Clear()
        {
            _hm.Clear();
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<WeakKey<TKey>, TValue>>)_hm).Contains(
                new KeyValuePair<WeakKey<TKey>, TValue>(new WeakKey<TKey>(item.Key), item.Value));
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((ICollection<KeyValuePair<WeakKey<TKey>, TValue>>)_hm).Remove(
                new KeyValuePair<WeakKey<TKey>, TValue>(new WeakKey<TKey>(item.Key), item.Value));
        }

        public int Count
        {
            get
            {
                CleanIfNeeded();
                return _hm.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool ContainsKey(TKey key)
        {
            return _hm.ContainsKey(new WeakKey<TKey>(key));
        }

        public void Add(TKey key, TValue value)
        {
            CleanIfNeeded();
            _hm.Add(new WeakKey<TKey>(key), value);
        }

        public bool Remove(TKey key)
        {
            return _hm.Remove(new WeakKey<TKey>(key));
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _hm.TryGetValue(new WeakKey<TKey>(key), out value);
        }

        public TValue this[TKey key]
        {
            get { return _hm[new WeakKey<TKey>(key)]; }
            set
            {
                CleanIfNeeded();
                _hm[new WeakKey<TKey>(key)] = value;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                CleanIfNeeded();
                return new KeyCollection(_hm);
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                CleanIfNeeded();
                return _hm.Values;
            }
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        #region KeyCollection
        class KeyCollection : ICollection<TKey>
        {
            private readonly HashMap<WeakKey<TKey>, TValue> _internalDict;

            public KeyCollection(HashMap<WeakKey<TKey>, TValue> dict)
            {
                _internalDict = dict;
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                foreach (var key in _internalDict.Keys)
                {
                    yield return key.Target;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                throw new NotImplementedException("Implement this as needed");
            }

            public int Count
            {
                get { return _internalDict.Count + 1; }
            }

            public bool IsReadOnly
            {
                get { return true; }
            }

            #region Explicit Interface Definitions
            bool ICollection<TKey>.Contains(TKey item)
            {
                throw new NotSupportedException();
            }

            void ICollection<TKey>.Add(TKey item)
            {
                throw new NotSupportedException();
            }

            void ICollection<TKey>.Clear()
            {
                throw new NotSupportedException();
            }

            bool ICollection<TKey>.Remove(TKey item)
            {
                throw new NotSupportedException();
            }
            #endregion
        }
        #endregion


        /// <summary>
        /// A weak reference wrapper for the hashtable keys. Whenever a key\value pair 
        /// is added to the hashtable, the key is wrapped using a WeakKey. WeakKey saves the
        /// value of the original object hashcode for fast comparison.
        /// </summary>
        class WeakKey<T>
        {
            WeakReference<T> reference;
            int hashCode;

            public WeakKey(T key)
            {
                if (key == null)
                    throw new ArgumentNullException("key");

                hashCode = key.GetHashCode();
                reference = new WeakReference<T>(key);
            }

            public override int GetHashCode()
            {
                return hashCode;
            }

            public override bool Equals(object obj)
            {
                if (!reference.IsAlive) return false;

                if (GetHashCode() != obj.GetHashCode()) return false;

                if (obj is WeakKey<T>)
                {
                    var other = (WeakKey<T>)obj;
                    return other.IsAlive && ReferenceEquals(reference.Target, other.Target);
                }

                return reference.Target.Equals(obj);
            }

            public T Target
            {
                get { return reference.Target; }
            }

            public bool IsAlive
            {
                get { return reference.IsAlive; }
            }
        }
    }
}