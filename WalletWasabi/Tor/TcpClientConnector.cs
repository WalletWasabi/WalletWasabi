using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor;

public static class TcpClientConnector
{
	/// <summary>
	/// Connects to end point using a TCP client.
	/// </summary>
	/// <param name="builder">Runs in between creation of TCP client and connection to the end point.</param>
	public static async Task<TcpClient> ConnectAsync(EndPoint endPoint, CancellationToken cancel, Action<TcpClient>? builder = null)
	{
		TcpClient tcpClient = new(endPoint.AddressFamily);
		builder?.Invoke(tcpClient);
		switch (endPoint)
		{
			case DnsEndPoint dnsEndPoint:
				await tcpClient.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port, cancel).ConfigureAwait(false);
				break;

			case IPEndPoint ipEndPoint:
				await tcpClient.ConnectAsync(ipEndPoint, cancel).ConfigureAwait(false);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(endPoint));
		}
		return tcpClient;
	}
}
