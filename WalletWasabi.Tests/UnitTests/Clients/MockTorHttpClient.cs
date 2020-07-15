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

		public Uri DestinationUri => new Uri("https://payment.server.org/pj");

		public Func<Uri> DestinationUriAction => () => DestinationUri;

		public EndPoint TorSocks5EndPoint => IPEndPoint.Parse("127.0.0.1:9050");

		public bool IsTorUsed => true;

		public Func<HttpMethod, string, string[], string, Task<HttpResponseMessage>> OnSendAsync { get; set; }

		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent content = null, CancellationToken cancel = default)
		{
			var body = (content is { })
				? await content.ReadAsStringAsync()
				: "";
			var sepPos = relativeUri.IndexOf('?');
			var action = relativeUri[..sepPos];
			var parameters = relativeUri[(sepPos + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
			return await OnSendAsync(method, action, parameters, body);
		}

		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			var relativeUri = request.RequestUri?.ToString()?.Replace(TorSocks5EndPoint.ToEndpointString(), "");
			var body = (request.Content is { })
				? await request.Content.ReadAsStringAsync()
				: "";

			var sepPos = relativeUri.IndexOf('?');
			var action = relativeUri[..sepPos];
			var parameters = relativeUri[(sepPos + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);
			return await OnSendAsync(request.Method, action, parameters, body);
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
