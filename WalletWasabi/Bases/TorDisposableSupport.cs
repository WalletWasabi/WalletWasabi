using System;
using System.Net;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Bases
{
	public class TorDisposableSupport : IDisposable
    {
		public TorHttpClient TorClient { get; }

		/// <param name="torSocks5EndPoint">if null, then localhost:9050</param>
		public TorDisposableSupport(Uri baseUri, IPEndPoint torSocks5EndPoint = null)
        {
			TorClient = new TorHttpClient(baseUri, torSocks5EndPoint, isolateStream: true);
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
