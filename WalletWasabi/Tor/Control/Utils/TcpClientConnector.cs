using System.Net;
using System.Net.Sockets;

namespace WalletWasabi.Tor.Control.Utils;

public class TcpClientConnector
{
	
	/// <summary>
	/// Connects to an endpoint using a TCP client.
	/// </summary>
	public static TcpClient Connect(EndPoint endPoint, Action<TcpClient>? builder = null)
	{
		TcpClient tcpClient = new();
		builder?.Invoke(tcpClient);
		switch (endPoint)
		{
			case DnsEndPoint dnsEndPoint:
				tcpClient.Connect(dnsEndPoint.Host, dnsEndPoint.Port);
				break;
			case IPEndPoint ipEndPoint:
				tcpClient.Connect(ipEndPoint);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(endPoint));
		}
		return tcpClient;
		
	}
}
