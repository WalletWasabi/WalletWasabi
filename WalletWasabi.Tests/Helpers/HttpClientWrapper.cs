using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.Helpers
{
	public class HttpClientWrapper : IHttpClient
	{
		private readonly HttpClient _httpClient;

		public HttpClientWrapper(HttpClient httpClient)
		{
			_httpClient = httpClient;
		}

		public Func<Uri>? BaseUriGetter => ()=> _httpClient.BaseAddress;

		public virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token = default)
		{
			return _httpClient.SendAsync(request, token);
		}
	}
}