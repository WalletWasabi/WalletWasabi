using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients.Wasabi;

public delegate DateTime LifetimeResolver(string identity);

public class HttpClientFactory : IHttpClientFactory
{
	class NotifyHttpClientHandler(string name, Action<string> disposedCallback) : HttpClientHandler
	{
		protected override void Dispose(bool disposing)
		{
			Logger.LogDebug($"Disposing httpclient handler {name}");
			base.Dispose(disposing);
			disposedCallback(name);
		}
	}

	private readonly ConcurrentDictionary<string, DateTime> _expirationDatetimes = new();
	private readonly ConcurrentDictionary<string, HttpClientHandler> _httpClientHandlers = new();
	private readonly ConcurrentBag<LifetimeResolver> _lifetimeResolvers = new();

	public HttpClientFactory()
	{
		AddLifetimeResolver(identity => identity.StartsWith("long-live")
			? DateTime.MaxValue
			: DateTime.UtcNow.AddHours(6));
	}

	public HttpClient CreateClient(string name)
	{
		CheckForExpirations();
		var httpClientHandler = _httpClientHandlers.GetOrAdd(name, CreateHttpClientHandler);
		return new HttpClient(httpClientHandler, false);
	}

	public void AddLifetimeResolver(LifetimeResolver resolver)
	{
		_lifetimeResolvers.Add(resolver);
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
		SetExpirationDate(name);
		return new NotifyHttpClientHandler(name,
			handlerName =>
			{
				_httpClientHandlers.TryRemove(handlerName, out _);
				_expirationDatetimes.TryRemove(handlerName, out _);
			});
	}

	private void SetExpirationDate(string name)
	{
		var expirationTime = _lifetimeResolvers.Min(resolve => resolve(name));
		_expirationDatetimes.AddOrUpdate(name, expirationTime, (_,_) => expirationTime);
	}
}

public class OnionHttpClientFactory(Uri proxyUri) : HttpClientFactory
{
	protected override HttpClientHandler CreateHttpClientHandler(string name)
	{
		var credentials = new NetworkCredential(name, name);
		var webProxy = new WebProxy(proxyUri, BypassOnLocal: false, [], Credentials: credentials);
		var handler = base.CreateHttpClientHandler(name);
		handler.Proxy = webProxy;
		return handler;
	}
}

public class CoordinatorHttpClientFactory : IHttpClientFactory
{
	private readonly Uri _baseAddress;
	private readonly HttpClientFactory _internalHttpClientFactory;

	public CoordinatorHttpClientFactory(Uri baseAddress, HttpClientFactory internalHttpClientFactory)
	{
		_baseAddress = baseAddress;
		_internalHttpClientFactory = internalHttpClientFactory;
		_internalHttpClientFactory.AddLifetimeResolver(ResolveLifetimeByIdentity);
	}

	public HttpClient CreateClient(string name)
	{
		var httpClient = _internalHttpClientFactory.CreateClient(name);
		httpClient.BaseAddress = _baseAddress;
		httpClient.DefaultRequestVersion = HttpVersion.Version11;
		httpClient.DefaultRequestHeaders.UserAgent.Clear();
		return httpClient;
	}

	private DateTime ResolveLifetimeByIdentity(string name)
	{
		var identity = name.Split('-', StringSplitOptions.RemoveEmptyEntries).First();
		var lifetime = TimeSpan.FromSeconds(identity switch
		{
			"bob" => 40,
			"alice" => 1.5 * 3_600,
			"satoshi" => int.MaxValue,
			_ => int.MaxValue,
		});
		return DateTime.UtcNow.Add(lifetime);
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
