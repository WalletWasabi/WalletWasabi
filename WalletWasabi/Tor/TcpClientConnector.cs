using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor;

public static class TcpClientConnector
{
	/// <summary>
	/// Opens a network stream with the end point.
	/// </summary>
	public static async Task<NetworkStream> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken)
	{
		Socket? socketToDispose = null;

		try
		{
			switch (endPoint)
			{
				case DnsEndPoint dnsEndPoint:
					socketToDispose = new Socket(SocketType.Stream, ProtocolType.Tcp);
					await socketToDispose.ConnectAsync(dnsEndPoint, cancellationToken).ConfigureAwait(false);
					break;

				case IPEndPoint ipEndPoint:
					socketToDispose = new Socket(SocketType.Stream, ProtocolType.Tcp);
					await socketToDispose.ConnectAsync(ipEndPoint, cancellationToken).ConfigureAwait(false);
					break;

				case UnixDomainSocketEndPoint unixDomainSocketEndPoint:
					socketToDispose = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
					await socketToDispose.ConnectAsync(unixDomainSocketEndPoint, cancellationToken).ConfigureAwait(false);
					break;

				default:
					throw new NotImplementedException();
			}

			var socket = socketToDispose;
			socketToDispose = null;

			return new NetworkStream(socket!, ownsSocket: true);
		}
		finally
		{
			socketToDispose?.Dispose();
		}
	}
}
