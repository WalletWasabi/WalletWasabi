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
	public static async Task<TcpClient> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken)
	{
		TcpClient? client = null;

		try
		{
			switch (endPoint)
			{
				case DnsEndPoint dnsEndPoint:
					client = new();
					await client.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
					break;

				case IPEndPoint ipEndPoint:
					client = new(endPoint.AddressFamily);
					await client.ConnectAsync(ipEndPoint, cancellationToken).ConfigureAwait(false);
					break;

				default:
					throw new NotSupportedException($"Endpoint of type '{endPoint.GetType().FullName}' is not supported.");
			}
		}
		catch
		{
			client?.Dispose();
			throw;
		}

		return client;
	}
}
