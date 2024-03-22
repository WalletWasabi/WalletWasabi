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
		catch (SocketException ex)
		{
			throw new TorConnectionException($"Could not connect to Tor SOCKSPort at '{endPoint}'. Is Tor running?", ex);
		}
	}
}
