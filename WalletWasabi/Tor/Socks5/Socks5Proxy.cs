using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Socks5;

public static class Socks5Proxy
{
	public static WebProxy? GetWebProxy(EndPoint? socksProxyEndPoint = null, ICredentials? credentials = null)
	{
		return socksProxyEndPoint switch
		{
			DnsEndPoint dns => TorWebProxy(dns.Host, dns.Port, credentials),
			IPEndPoint ip => TorWebProxy(ip.Address.ToString(), ip.Port, credentials),
			null => null,
			_ => throw new NotSupportedException("The endpoint type is not supported.")
		};
	}

	private static WebProxy TorWebProxy(string host, int port, ICredentials? credentials)
	{
		return new(new UriBuilder("socks5", host, port).Uri)
		{
			Credentials = credentials,
		};
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClientHandler is set to be disposed by the HttpClient instance.")]
	public static HttpClient CreateHttpClient(bool enableProxy, EndPoint? proxyEndpoint = null, Uri? baseAddress = null, TimeSpan? pooledConnectionLifetime = null)
	{
		IWebProxy? proxy = enableProxy
			? GetWebProxy(proxyEndpoint, new NetworkCredential(DefaultCircuit.Instance.Name, DefaultCircuit.Instance.Name))
			: null;

		// HttpClientHandler httpClientHandler = new();
		SocketsHttpHandler handler = new()
		{
			AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli,
			PooledConnectionLifetime = pooledConnectionLifetime ?? TimeSpan.FromMinutes(5),
			Proxy = proxy
		};

		HttpClient client = new(handler, disposeHandler: true);
		client.BaseAddress = baseAddress;

		return client;
	}

}
