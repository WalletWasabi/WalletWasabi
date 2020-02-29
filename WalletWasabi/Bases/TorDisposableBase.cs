using System;
using System.Net;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Bases
{
	public abstract class TorDisposableBase : IDisposable
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		/// <param name="torSocks5EndPoint">If null, then localhost:9050</param>
		protected TorDisposableBase(Uri baseUri, EndPoint torSocks5EndPoint)
		{
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
		}

		/// <param name="torSocks5EndPoint">If null, then localhost:9050</param>
		protected TorDisposableBase(Func<Uri> baseUriAction, EndPoint torSocks5EndPoint)
		{
			TorClient = new TorHttpClient(baseUriAction, torSocks5EndPoint, isolateStream: true);
		}

		protected TorDisposableBase(ITorHttpClient torClient)
		{
			TorClient = torClient;
		}

		public ITorHttpClient TorClient { get; }

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					TorClient?.Dispose();
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
