using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Exceptions;

namespace WalletWasabi.Tor.Socks5;

public static class TcpClientSocks5Connector
{
	/// <summary>
	/// Establishes TCP connection with Tor SOCKS5 endpoint.
	/// </summary>
	/// <param name="builder">Runs in between creation of TCP client and connection to the end point.</param>
	/// <exception cref="ArgumentException">This should never happen.</exception>
	/// <exception cref="TorException">When connection to Tor SOCKS5 endpoint fails.</exception>
	public static async Task<TcpClient> ConnectAsync(EndPoint endPoint, CancellationToken cancel, Action<TcpClient>? builder = null)
	{
		try
		{
			return await TcpClientConnector.ConnectAsync(endPoint, cancel, builder).ConfigureAwait(false);
		}
		catch (SocketException ex) when (ex.ErrorCode is 10061 or 111 or 61)
		{
			// 10061 ~ "No connection could be made because the target machine actively refused it" on Windows.
			// 111   ~ "Connection refused" on Linux.
			// 61    ~ "Connection refused" on macOS.
			throw new TorConnectionException($"Could not connect to Tor SOCKSPort at '{endPoint}'. Is Tor running?", ex);
		}
	}
}
