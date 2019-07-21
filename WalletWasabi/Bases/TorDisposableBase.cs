using System;
using System.Net;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Bases
{
	public abstract class TorDisposableBase : IDisposable
	{
		public TorHttpClient TorClient { get; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		protected TorDisposableBase(Uri baseUri, IPEndPoint torSocks5EndPoint)
		{
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
		}

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		protected TorDisposableBase(Func<Uri> baseUriAction, IPEndPoint torSocks5EndPoint)
		{
			TorClient = new TorHttpClient(baseUriAction, torSocks5EndPoint, isolateStream: true);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

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
