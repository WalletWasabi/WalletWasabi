using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Exceptions;

namespace WalletWasabi.Tor.Socks5;

public static class TcpClientSocks5Connector
{
	/// <summary>
	/// Establishes TCP connection with Tor SOCKS5 endpoint.
	/// </summary>
	/// <exception cref="NotSupportedException">This should never happen.</exception>
	/// <exception cref="TorException">When connection to Tor SOCKS5 endpoint fails.</exception>
	public static async Task<TcpClient> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken)
	{
		try
		{
			return await TcpClientConnector.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
		}
		catch (SocketException ex) when (ex.ErrorCode is 10061 or 111 or 104 or 61)
		{
			// 10061 ~ "No connection could be made because the target machine actively refused it" on Windows.
			// 111   ~ "Connection refused" on Linux.
			// 104   ~ "connection reset by peer" on Linux.
			// 61    ~ "Connection refused" on macOS.
			throw new TorConnectionException($"Could not connect to Tor SOCKSPort at '{endPoint}'. Is Tor running?", ex);
		}
	}
}
