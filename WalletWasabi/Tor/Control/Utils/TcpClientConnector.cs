using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tor.Control.Utils;

public static class TcpClientConnector
{
	/// <summary>
	/// Connects to Tor control using a TCP client.
	/// </summary>
	public static TcpClient Connect(EndPoint endPoint)
	{
		TcpClient tcpClient = new();
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
