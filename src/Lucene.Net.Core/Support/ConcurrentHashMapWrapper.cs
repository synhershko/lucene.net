﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace Lucene.Net.Support
{
    public class ConcurrentHashMapWrapper<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly IDictionary<TKey, TValue> _dict;

        public ConcurrentHashMapWrapper(IDictionary<TKey, TValue> wrapped)
        {
            this._dict = wrapped;
        }

        public void Add(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                _dict.Add(key, value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsKey(TKey key)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return new ReadOnlyCollection<TKey>(_dict.Keys.ToArray());
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                return _dict.Remove(key);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.TryGetValue(key, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return new ReadOnlyCollection<TValue>(_dict.Values.ToArray());
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dict[key];
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
            set
            {
                _lock.EnterWriteLock();
                try
                {
                    _dict[key] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            _lock.EnterWriteLock();
            try
            {
                _dict.Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _dict.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            _lock.EnterReadLock();
            try
            {
                _dict.CopyTo(array, arrayIndex);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dict.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool IsReadOnly
        {
            get { return _dict.IsReadOnly; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            _lock.EnterWriteLock();
            try
            {
                return _dict.Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotSupportedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotSupportedException();
        }
    }
}