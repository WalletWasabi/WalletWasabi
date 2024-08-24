using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.WebClients.Wasabi;

public class HttpClientFactory : IHttpClientFactory
{
	class NotifyingHttpClientHandler(string name, Action<string> disposedCallback) : HttpClientHandler
	{
		protected override void Dispose(bool disposing)
		{
			Logger.LogInfo("disposing httpclient handler");
			base.Dispose(disposing);
			disposedCallback(name);
		}
	}

    private readonly ConcurrentDictionary<string, HttpClientHandler> _httpClientHandlers = new();

	public HttpClient CreateClient(string name)
	{
		var httpClientHandler = _httpClientHandlers.GetOrAdd(name, CreateHttpClientHandler);
		return new HttpClient(httpClientHandler, false);
	}

	protected virtual HttpClientHandler CreateHttpClientHandler(string name)
	{
		return new NotifyingHttpClientHandler(name,
			handlerName => { } /*_httpClientHandlers.TryRemove(handlerName, out _)*/);
	}
}

public class OnionHttpClientFactory(Uri proxyUri) : HttpClientFactory
{
	protected override HttpClientHandler CreateHttpClientHandler(string name)
	{
		var credentials = new NetworkCredential(name, name);
		var webProxy = new WebProxy(proxyUri, BypassOnLocal: true, [], Credentials: credentials);
		var handler = base.CreateHttpClientHandler(name);
		handler.Proxy = webProxy;
		return handler;
	}
}

public class CoordinatorHttpClientFactory(Uri baseAddress, IHttpClientFactory internalHttpClientFactory)
	: IHttpClientFactory
{
	public HttpClient CreateClient(string name)
	{
		var httpClient = internalHttpClientFactory.CreateClient(name);
		httpClient.BaseAddress = baseAddress;
		return httpClient;
	}
}

public class BackendHttpClientFactory(Uri baseAddress, IHttpClientFactory internalHttpClientFactory)
	: IHttpClientFactory
{
	public HttpClient CreateClient(string name)
	{
		var httpClient = internalHttpClientFactory.CreateClient(name);
		httpClient.BaseAddress = baseAddress;
		return httpClient;
	}
}
