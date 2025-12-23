using Org.BouncyCastle.Bcpg;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Tor;

public static class TcpClientConnector
{
	/// <summary>
	/// Connects to end point using a TCP client.
	/// </summary>
	public static async Task<NetworkStream> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken)
	{
		try
		{
			switch (endPoint)
			{
				case DnsEndPoint dnsEndPoint:
				{
					var client = new TcpClient();
					await client.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port, cancellationToken).ConfigureAwait(false);

					return client.GetStream();
				}

				case IPEndPoint ipEndPoint:
				{
					var client = new TcpClient(endPoint.AddressFamily);
					await client.ConnectAsync(ipEndPoint, cancellationToken).ConfigureAwait(false);
					return client.GetStream();
				}

				case UnixDomainSocketEndPoint unixDomainSocketEndPoint:
					var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
					await socket.ConnectAsync(unixDomainSocketEndPoint, cancellationToken).ConfigureAwait(false);
					return new NetworkStream(socket, ownsSocket: true);

				default:
					throw new NotSupportedException($"Endpoint of type '{endPoint.GetType().FullName}' is not supported.");
			}
		}
		catch
		{
			throw;
		}
	}
}
