using System.Net;

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
}
