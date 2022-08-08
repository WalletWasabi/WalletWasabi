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
			client = new(endPoint.AddressFamily);

			switch (endPoint)
			{
				case DnsEndPoint dnsEndPoint:
					await client.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
					break;
				case IPEndPoint ipEndPoint:
					await client.ConnectAsync(ipEndPoint, cancellationToken).ConfigureAwait(false);
					break;
				default:
					throw new NotSupportedException($"Endpoint of type '{endPoint.GetType().FullName}' is not supported.");
			}
		}
		catch (Exception)
		{
			client?.Dispose();
			throw;
		}

		return client;
	}
}
