using System;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Socks5.Pool;

namespace WalletWasabi.Tor.Socks5
{
	/// <summary>
	/// TODO.
	/// </summary>
	public class ClientsManager
	{
		public ClientsManager(int maxPoolItemsPerHost)
		{
			Clients = new Dictionary<string, List<IPoolItem>>();
			MaxPoolItemsPerHost = maxPoolItemsPerHost;
		}

		/// <summary>Provider of exclusive access to <see cref="Clients"/>.</summary>
		private object ClientsLock { get; } = new object();

		/// <summary>Key is always a URI host. Value is a list of pool items that can connect to the URI host.</summary>
		/// <remarks>All access to this object must be guarded by <see cref="ClientsLock"/>.</remarks>
		private Dictionary<string, List<IPoolItem>> Clients { get; }
		public int MaxPoolItemsPerHost { get; }

		private bool _disposedValue;

		/// <summary>
		/// TODO.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="poolItem"></param>
		/// <returns><c>true</c> when the new item was added, <c>false</c> otherwise.</returns>
		public bool AddPoolItem(string host, IPoolItem poolItem)
		{
			lock (ClientsLock)
			{
				// Get list of connections for given host.
				List<IPoolItem> hostItems = GetHostListNoLock(host);

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
		/// TODO.
		/// </summary>
		/// <param name="host">TODO.</param>
		public List<IPoolItem> GetItemsCopy(string host)
		{
			lock (ClientsLock)
			{
				return new List<IPoolItem>(GetHostListNoLock(host));
			}
		}

		public (bool canBeAdded, IPoolItem? poolItem) GetPoolItem(string host, bool isolateStream)
		{
			lock (ClientsLock)
			{
				// Get list of connections for given host.
				List<IPoolItem> hostItems = GetHostListNoLock(host);

				IPoolItem? reservedItem = null;

				// Find first free connection, if it exists.a
				List<IPoolItem> disposeList = hostItems.FindAll(item => item.NeedRecycling()).ToList();

				// Remove items for disposal from the list.
				disposeList.ForEach(item => hostItems.Remove(item));

				if (!isolateStream)
				{
					// Find first free connection, if it exists.
					reservedItem = hostItems.Find(item => item.TryReserve());
				}
				else
				{
					Logger.LogTrace($"['{host}'] Isolate stream requested. No pool item re-using.");
				}

				bool canBeAdded = hostItems.Count < MaxPoolItemsPerHost;

				return (canBeAdded, reservedItem);
			}
		}

		/// <remarks>Requires access guarded by <see cref="ClientsLock"/>.</remarks>
		private List<IPoolItem> GetHostListNoLock(string host)
		{
			// Make sure the list is present.
			if (!Clients.ContainsKey(host))
			{
				Clients.Add(host, new List<IPoolItem>());
			}

			return Clients[host];
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
					foreach (List<IPoolItem> list in Clients.Values)
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