using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tor;

public static class TcpClientConnector
{
	/// <summary>
	/// Connects to end point using a TCP client.
	/// </summary>
	public static async Task<TcpClient> ConnectAsync(EndPoint endPoint)
	{
		TcpClient tcpClient = new();
		switch (endPoint)
		{
			case DnsEndPoint dnsEndPoint:
				await tcpClient.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port).ConfigureAwait(false);
				break;

			case IPEndPoint ipEndPoint:
				await tcpClient.ConnectAsync(ipEndPoint).ConfigureAwait(false);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(endPoint));
		}
		return tcpClient;
	}
}
