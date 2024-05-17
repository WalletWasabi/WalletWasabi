using System.Net;

namespace WalletWasabi.Tor.Socks5;

public static class Socks5Proxy
{
	public static WebProxy? GetWebProxy(EndPoint? socksProxyEndPoint = null)
	{
		return socksProxyEndPoint switch
		{
			DnsEndPoint dns => TorWebProxy(dns.Host, dns.Port),
			IPEndPoint ip => TorWebProxy(ip.Address.ToString(), ip.Port),
			null => null,
			_ => throw new NotSupportedException("The endpoint type is not supported.")
		};
		static WebProxy TorWebProxy(string host, int port) => new(new UriBuilder("socks5", host, port).Uri);
	}
}
