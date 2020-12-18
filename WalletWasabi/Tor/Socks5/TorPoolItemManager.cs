using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// Associates <see cref="IPoolItem"/> with <see cref="PoolItemState"/> and manages pool items re-usability.
	/// </summary>
	/// <remarks>The class is thread-safe.</remarks>
	public class TorPoolItemManager : IDisposable
	{
		private bool _disposedValue;

		public TorPoolItemManager(int maxPoolItemsPerHost)
		{
			HostBuckets = new Dictionary<string, List<IPoolItem>>();
			MaxPoolItemsPerHost = maxPoolItemsPerHost;
		}

		/// <summary>Provider of exclusive access to <see cref="HostBuckets"/>.</summary>
		private object HostBucketsLock { get; } = new object();

		/// <summary>Key is always a URI host. Value is a list of pool items that can connect to the URI host.</summary>
		/// <remarks>All access to this object must be guarded by <see cref="HostBucketsLock"/>.</remarks>
		private Dictionary<string, List<IPoolItem>> HostBuckets { get; }

		public int MaxPoolItemsPerHost { get; }

		/// <summary>
		/// Adds <paramref name="poolItem"/> to <see cref="HostBuckets"/>.
		/// </summary>
		/// <param name="host">URI's host.</param>
		/// <param name="poolItem">Pool item to add.</param>
		/// <returns><c>true</c> when the new item was added, <c>false</c> otherwise.</returns>
		public bool TryAddPoolItem(string host, IPoolItem poolItem)
		{
			lock (HostBucketsLock)
			{
				// Get list of connections for given host.
				List<IPoolItem> hostItems = AddOrGetNoLock(host);

				if (hostItems.Count < MaxPoolItemsPerHost)
				{
					hostItems.Add(poolItem);
					return true;
				}
				else
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Gets all <see cref="IPoolItem"/>s which are related to URI <paramref name="host"/>.
		/// </summary>
		/// <param name="host">A <see cref="Uri.Host"/> value is expected.</param>
		public List<IPoolItem> GetItemsCopy(string host)
		{
			lock (HostBucketsLock)
			{
				return new List<IPoolItem>(AddOrGetNoLock(host));
			}
		}

		/// <summary>
		/// Gets reserved pool item to use, if any.
		/// </summary>
		/// <param name="host">URI's host value.</param>
		/// <param name="isolateStream"><c>true</c> if a new Tor circuit is required for this HTTP request.</param>
		/// <returns>Whether a new pool item can be added to <see cref="TorPoolItemManager"/> and reserved pool item to use, if any.</returns>
		public bool GetPoolItem(string host, bool isolateStream, out IPoolItem? poolItem)
		{
			lock (HostBucketsLock)
			{
				// Get list of connections for given host.
				List<IPoolItem> hostItems = AddOrGetNoLock(host);

				poolItem = null;

				// Find first free connection, if it exists.
				List<IPoolItem> disposeList = hostItems.FindAll(item => item.NeedRecycling).ToList();

				// Remove items for disposal from the list.
				disposeList.ForEach(item =>
				{
					hostItems.Remove(item);
					(item as IDisposable)?.Dispose();
				});

				if (!isolateStream)
				{
					// Find first free connection, if it exists.
					poolItem = hostItems.Find(item => item.TryReserve());
				}
				else
				{
					Logger.LogTrace($"['{host}'] Isolate stream requested. No pool item re-using.");
				}

				bool canBeAdded = hostItems.Count < MaxPoolItemsPerHost;

				return canBeAdded;
			}
		}

		/// <remarks>Requires access guarded by <see cref="HostBucketsLock"/>.</remarks>
		private List<IPoolItem> AddOrGetNoLock(string host)
		{
			// Make sure the list is present.
			if (!HostBuckets.ContainsKey(host))
			{
				HostBuckets.Add(host, new List<IPoolItem>());
			}

			return HostBuckets[host];
		}

		/// <summary>
		/// <list type="bullet">
		/// <item>Unmanaged resources need to be released regardless of the value of the <paramref name="disposing"/> parameter.</item>
		/// <item>Managed resources need to be released if the value of <paramref name="disposing"/> is <c>true</c>.</item>
		/// </list>
		/// </summary>
		/// <param name="disposing">
		/// Indicates whether the method call comes from a <see cref="Dispose()"/> method
		/// (its value is <c>true</c>) or from a finalizer (its value is <c>false</c>).
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					foreach (List<IPoolItem> list in HostBuckets.Values)
					{
						foreach (IPoolItem item in list)
						{
							(item as IDisposable)?.Dispose();
						}
					}
				}
				_disposedValue = true;
			}
		}

		/// <summary>
		/// Do not change this code.
		/// </summary>
		public void Dispose()
		{
			// Dispose of unmanaged resources.
			Dispose(true);

			// Suppress finalization.
			GC.SuppressFinalize(this);
		}
	}
}