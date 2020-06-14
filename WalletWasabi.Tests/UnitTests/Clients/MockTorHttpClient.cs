using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class MockTorHttpClient : ITorHttpClient
	{
		private volatile bool _disposedValue = false; // To detect redundant calls

		public Uri DestinationUri => new Uri("DestinationUri");

		public Func<Uri> DestinationUriAction => () => DestinationUri;

		public EndPoint TorSocks5EndPoint => IPEndPoint.Parse("127.0.0.1:9050");

		public bool IsTorUsed => true;

		public Func<HttpMethod, string, string[], Task<HttpResponseMessage>> OnSendAsync_Method { get; set; }
		public Func<HttpMethod, string, string, Task<HttpResponseMessage>> OnSendAsync { get; set; }

		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default)
		{
			if (string.IsNullOrWhiteSpace(relativeUri))
			{
				return await OnSendAsync(method, "", await content.ReadAsStringAsync());
			}
			else
			{
				var sepPos = relativeUri.IndexOf('?');
				var action = relativeUri[..sepPos];
				var parameters = relativeUri[(sepPos + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
				return await OnSendAsync_Method(method, action, parameters);
			}
		}

		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			var relativeUri = request.RequestUri?.ToString()?.Replace(TorSocks5EndPoint.ToEndpointString(), "");
			if (string.IsNullOrWhiteSpace(relativeUri))
			{
				return await OnSendAsync(request.Method, "", await request.Content.ReadAsStringAsync());
			}
			else
			{
				var sepPos = relativeUri.IndexOf('?');
				var action = relativeUri[..sepPos];
				var parameters = relativeUri[(sepPos + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
				return await OnSendAsync_Method(request.Method, action, parameters);
			}
		}

		#region IDisposable Support

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
