using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Secp256k1;
using NNostr.Client;

namespace WalletWasabi.Discoverability;

public static class NostrExtensions
{
	public static INostrClient Create(Uri[] relays, EndPoint? torEndpoint = null)
	{
		var webProxy = torEndpoint is IPEndPoint endpoint
			? new WebProxy($"socks5://{endpoint.Address}:{endpoint.Port}")
			: torEndpoint is DnsEndPoint endpoint2
				? new WebProxy($"socks5://{endpoint2.Host}:{endpoint2.Port}")
				: null;
		return Create(relays, webProxy);
	}

	public static INostrClient Create(Uri[] relays, WebProxy? proxy = null)
	{
		void ConfigureSocket(WebSocket socket)
		{
			if (socket is ClientWebSocket clientWebSocket && proxy != null)
			{
				clientWebSocket.Options.Proxy = proxy;
			}
		}

		return relays.Length switch
		{
			0 => throw new ArgumentException("At least one relay is required.", nameof(relays)),
			1 => new NostrClient(relays.First(), ConfigureSocket),
			_ => new CompositeNostrClient(relays, ConfigureSocket)
		};
	}

	public static async Task PublishAsync(
		this INostrClient client,
		NostrEvent[] evts,
		CancellationToken cancellationToken)
	{
		if (!evts.Any())
		{
			return;
		}

		await client.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);
		await client.SendEventsAndWaitUntilReceived(evts, cancellationToken).ConfigureAwait(false);
		await client.Disconnect().ConfigureAwait(false);
	}
}
