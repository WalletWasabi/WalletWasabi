using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients.Wasabi;

public class HttpClientFactory : IHttpClientFactory
{
	class NotifyHttpClientHandler(string name, Action<string> disposedCallback) : HttpClientHandler
	{
		protected override void Dispose(bool disposing)
		{
			Logger.LogDebug($"disposing httpclient handler {name}");
			base.Dispose(disposing);
			disposedCallback(name);
		}
	}

	private readonly ConcurrentDictionary<string, DateTime> _expirationDatetimes = new();
    private readonly ConcurrentDictionary<string, HttpClientHandler> _httpClientHandlers = new();

	public HttpClient CreateClient(string name)
	{
		CheckForExpirations();
		var httpClientHandler = _httpClientHandlers.GetOrAdd(name, CreateHttpClientHandler);
		return new HttpClient(httpClientHandler, false);
	}

	private void CheckForExpirations()
	{
		var expiredHandlers = _expirationDatetimes.Where(x => x.Value < DateTime.UtcNow).Select(x => x.Key).ToArray();
		foreach (var handlerName in expiredHandlers)
		{
			if (_httpClientHandlers.TryRemove(handlerName, out var handler))
			{
				handler.Dispose();
			}
		}
	}

	protected virtual HttpClientHandler CreateHttpClientHandler(string name)
	{
		Logger.LogDebug($"Create handler {name}");
		return new NotifyHttpClientHandler(name,
			handlerName =>
			{
				_httpClientHandlers.TryRemove(handlerName, out _);
				_expirationDatetimes.TryRemove(handlerName, out _);
			});
	}

	internal void SetExpirationDate(string name, DateTime dueDate)
	{
		_expirationDatetimes.TryAdd(name, dueDate);
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

public class CoordinatorHttpClientFactory(Uri baseAddress, HttpClientFactory internalHttpClientFactory)
	: IHttpClientFactory
{
	public HttpClient CreateClient(string name)
	{
		var identity = name.Split('-', StringSplitOptions.RemoveEmptyEntries).First();
		var lifetime = TimeSpan.FromSeconds(identity switch
		{
			"bob" => 10,
			"alice" => 1.5 * 3_600,
			"satoshi" => int.MaxValue,
			var other => throw new ArgumentException($"Unknown identity '{other}'.")
		});
		var httpClient = internalHttpClientFactory.CreateClient(name);
		internalHttpClientFactory.SetExpirationDate(name, DateTime.UtcNow.Add(lifetime));
		httpClient.BaseAddress = baseAddress;
		httpClient.DefaultRequestVersion = HttpVersion.Version11;
		httpClient.DefaultRequestHeaders.UserAgent.Clear();
		return httpClient;
	}
}

public class BackendHttpClientFactory(Uri baseAddress, IHttpClientFactory internalHttpClientFactory)
	: IHttpClientFactory
{
	public HttpClient CreateClient(string name)
	{
		var httpClient = internalHttpClientFactory.CreateClient(name);
		httpClient.DefaultRequestVersion = HttpVersion.Version11;
		httpClient.DefaultRequestHeaders.UserAgent.Clear();
		httpClient.BaseAddress = baseAddress;
		return httpClient;
	}
}
