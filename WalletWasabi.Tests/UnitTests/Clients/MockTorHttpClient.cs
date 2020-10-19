using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WalletWasabi.Tor.Http.Interfaces;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class MockTorHttpClient : ITorHttpClient
	{
		public Uri DestinationUri => new Uri("https://payment.server.org/pj");

		public Func<Uri> DestinationUriAction => () => DestinationUri;

		public EndPoint TorSocks5EndPoint => IPEndPoint.Parse("127.0.0.1:9050");

		public bool IsTorUsed => true;

		public Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>> OnSendAsync { get; set; }

		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			string body = (content is { })
				? await content.ReadAsStringAsync().ConfigureAwait(false)
				: "";

			// It does not matter which URI is actually used here, we just need to construct absolute URI to be able to access `uri.Query`.
			Uri baseUri = new Uri("http://127.0.0.1");
			Uri uri = new Uri(baseUri, relativeUri);
			NameValueCollection parameters = HttpUtility.ParseQueryString(uri.Query);

			return await OnSendAsync(method, uri.AbsolutePath, parameters, body).ConfigureAwait(false);
		}

		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel = default)
		{
			string body = (request.Content is { })
				? await request.Content.ReadAsStringAsync().ConfigureAwait(false)
				: "";

			Uri uri = request.RequestUri;
			NameValueCollection parameters = HttpUtility.ParseQueryString(uri.Query);

			return await OnSendAsync(request.Method, uri.AbsolutePath, parameters, body);
		}
	}
}
