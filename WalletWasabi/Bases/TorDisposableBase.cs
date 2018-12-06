using System;
using System.Collections.Generic;
using System.Net;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Bases
{
	public abstract class TorDisposableBase : IDisposable
	{
		private static List<TorDisposableBase> Clients = new List<TorDisposableBase>();
		private static object sync = new object();

		public TorHttpClient TorClient { get; private set; }
		private TorHttpClient _torHSClient;
		private TorHttpClient _torCNClient;

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		protected TorDisposableBase(Uri baseUri, Uri backupUri, IPEndPoint torSocks5EndPoint = null)
		{
			_torHSClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
			_torCNClient = new TorHttpClient(backupUri, torSocks5EndPoint, isolateStream: true);
			TorClient = _torHSClient;
			
			lock(sync) Clients.Add(this);
		}

		public TorDisposableBase()
		{
		}

		private void UseFallbackClient()
		{
			TorClient = _torCNClient;
		}

		public static void UseFallbackClients()
		{
			lock(sync)
			{
				foreach(var client in Clients)
				{
					client.UseFallbackClient();
				}
			}
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_torHSClient?.Dispose();
					_torCNClient?.Dispose();
					lock(sync) Clients.Remove(this);
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
