using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.UnitTests.Clients
{
	public class MockTorHttpClient : IHttpClient
	{
		public Func<Uri> BaseUriGetter => () => new Uri("https://payment.server.org/pj");

		public Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>> OnSendAsync { get; set; }

		public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancel = default)
		{
			string body = (content is { })
				? await content.ReadAsStringAsync(cancel).ConfigureAwait(false)
				: "";

			// It does not matter which URI is actually used here, we just need to construct absolute URI to be able to access `uri.Query`.
			Uri baseUri = new Uri("http://127.0.0.1");
			Uri uri = new Uri(baseUri, relativeUri);
			NameValueCollection parameters = HttpUtility.ParseQueryString(uri.Query);

			return await OnSendAsync(method, uri.AbsolutePath, parameters, body).ConfigureAwait(false);
		}

		public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			string body = (request.Content is { })
				? await request.Content.ReadAsStringAsync(token).ConfigureAwait(false)
				: "";

			Uri uri = request.RequestUri!;
			NameValueCollection parameters = HttpUtility.ParseQueryString(uri.Query);

			return await OnSendAsync(request.Method, uri.AbsolutePath, parameters, body).ConfigureAwait(false);
		}
	}
}