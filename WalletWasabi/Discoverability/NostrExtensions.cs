using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NNostr.Client;

namespace WalletWasabi.Discoverability;

public static class NostrClientFactory
{
	public static INostrClient Create(Uri[] relays, EndPoint? torEndpoint = null)
	{
		var webProxy = torEndpoint switch
		{
			IPEndPoint endpoint => new WebProxy($"socks5://{endpoint.Address}:{endpoint.Port}"),
			DnsEndPoint endpoint => new WebProxy($"socks5://{endpoint.Host}:{endpoint.Port}"),
			null => null,
			_ => throw new ArgumentException("Endpoint type is not supported.")
		};
		return Create(relays, webProxy);
	}

	public static INostrClient Create(Uri[] relays, WebProxy? proxy)
	{
		return relays.Length switch
		{
			0 => throw new ArgumentException("At least one relay is required.", nameof(relays)),
			1 => new NostrClient(relays.First(), ConfigureSocket),
			_ => new CompositeNostrClient(relays, ConfigureSocket)
		};

		void ConfigureSocket(WebSocket socket)
		{
			if (socket is ClientWebSocket clientWebSocket)
			{
				clientWebSocket.Options.Proxy = proxy;
			}
		}
	}
}

public static class NostrExtensions
{
	public static async Task PublishAsync(
		this INostrClient client,
		NostrEvent[] events,
		CancellationToken cancellationToken)
	{
		await client.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);
		await client.SendEventsAndWaitUntilReceived(events, cancellationToken).ConfigureAwait(false);
		await client.Disconnect().ConfigureAwait(false);
	}
}
