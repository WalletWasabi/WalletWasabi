using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace WalletWasabi.Cache;

public class MemoryCache<TKey, TValue> where TKey : notnull
{
	private readonly record struct ExpirableValue
	{
		public readonly TValue Value;
		public readonly long WhenToEvict;

		public ExpirableValue(TValue value, TimeSpan expiration)
		{
			Value = value;
			WhenToEvict = Environment.TickCount64 + (long) expiration.TotalMilliseconds;
		}

		public bool IsExpired => Environment.TickCount64 > WhenToEvict;
	}

	private readonly object _syncObj = new();
	private readonly ConcurrentDictionary<TKey, ExpirableValue> _internalDictionary = new();

	// ReSharper disable once NotAccessedField.Local
	private readonly Timer _evictionTimer;

	public MemoryCache(TimeSpan evictionInterval)
	{
		void Evict(object? _) => EvictExpiredCallback();
		_evictionTimer = new Timer(
			callback: Evict,
			state: null,
			dueTime: evictionInterval,
			period: evictionInterval);
	}

	public bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value)
	{
		if (_internalDictionary.TryGetValue(key, out var expirationValue))
		{
			if (expirationValue.IsExpired)
			{
				KeyValuePair<TKey, ExpirableValue>
					kv = new KeyValuePair<TKey, ExpirableValue>(key, expirationValue);
				_internalDictionary.TryRemove(kv);
				value = default;
				return false;
			}

			value = expirationValue!.Value!;
			return true;
		}

		value = default;
		return false;
	}

	public bool TryAdd(TKey key, TValue value, TimeSpan expiration) =>
		!TryGet(key, out _) && _internalDictionary.TryAdd(key, new ExpirableValue(value, expiration));

	private void EvictExpiredCallback()
	{
		lock (_syncObj)
		{
			var currTime = Environment.TickCount64;

			foreach (var p in _internalDictionary)
			{
				if (currTime > p.Value.WhenToEvict)
				{
					_internalDictionary.TryRemove(p);
				}
			}
		}
	}
}
