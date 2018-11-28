using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Models
{
	[DebuggerDisplay("Count = {Count}")]
    public class ConcurrentHashSet<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
    {
        private readonly ConcurrentDictionary<T, byte> _dict = new ConcurrentDictionary<T, byte>();

        public int Count 
			=> _dict.Count;

        public void Clear()
			=> _dict.Clear();

        public bool Contains(T item)
			=> _dict.ContainsKey(item);

        public bool TryAdd(T item)
            => _dict.TryAdd(item, 0);

        public bool TryRemove(T item)
			=> _dict.TryRemove(item, out byte dontCare);

        public IEnumerator<T> GetEnumerator()
            => _dict.Keys.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => _dict.Keys.GetEnumerator();
    }
}
