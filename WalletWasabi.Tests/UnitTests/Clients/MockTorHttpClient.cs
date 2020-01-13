using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class MockTorHttpClient : ITorHttpClient
	{
		public Uri DestinationUri => new Uri("DestinationUri");

		public Func<Uri> DestinationUriAction => () => DestinationUri;

		public EndPoint TorSocks5EndPoint => IPEndPoint.Parse("127.0.0.1:9050");

		public bool IsTorUsed => true;

		public Func<HttpMethod, string, string[], Task<HttpResponseMessage>> OnSendAsync_Method { get; set; }

		public Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default)
		{
			var sepPos = relativeUri.IndexOf('?');
			var action = relativeUri[..sepPos];
			var parameters = relativeUri[(sepPos + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
			return OnSendAsync_Method(method, action, parameters);
		}

		public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			throw new NotImplementedException();
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
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
