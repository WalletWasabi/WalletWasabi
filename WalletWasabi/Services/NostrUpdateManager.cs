using NNostr.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using System.Net.Http;
using WalletWasabi.Tor;
using NNostr.Client.Protocols;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Logging;
using UpdateStatus = (bool ClientUpToDate, bool IsReadyToInstall, System.Version ClientVersion);

namespace WalletWasabi.Services;
public class NostrUpdateManager : PeriodicRunner
{
	public event EventHandler<UpdateStatus>? UpdateAvailableToGet;

	public const string DefaultPublicKey = "npub1l0p8r79n24ez6ahh93utyyu268hj7cg3gdsql4526rwlc6qhxx3sxy0yeu";	// Test nostr profile, switch it with OG
	private INostrClient? _nostrClient;
	private string? _nostrSubscriptionID;
	private EndPoint _torEndpoint;

	public NostrUpdateManager(TimeSpan period, EndPoint endPoint) : base(period)
	{
		_torEndpoint = endPoint;
	}

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId == _nostrSubscriptionID)
		{
			foreach (var nostrEvents in args.events)
			{
				if (nostrEvents is not null)
				{
					Logger.LogInfo(nostrEvents.Id);
					Logger.LogInfo("Content: " + nostrEvents.Content);
					Logger.LogInfo("Kind: " + nostrEvents.Kind.ToString());
					Logger.LogInfo("Created at: " + nostrEvents.CreatedAt.ToString());

					foreach (var tag in nostrEvents.Tags)
					{
						Logger.LogInfo($"{tag.TagIdentifier} - {tag.Data}");
					}
				}
			}
		}
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		await CheckNostrConnectionAsync(cancel).ConfigureAwait(false);
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		if (_nostrClient is not null)
		{
			try
			{
				_nostrClient.Disconnect();
			}
			catch (Exception){}

			_nostrClient.Dispose();
			_nostrClient = null;
		}
		return base.StopAsync(cancellationToken);
	}

	private async Task CheckNostrConnectionAsync(CancellationToken cancel)
	{
		if (_nostrClient is null)
		{
			try
			{
				string defaultPubKeyHex = NIP19.FromNIP19Npub(DefaultPublicKey).ToHex();

				string[] relayUrls = ["wss://relay.primal.net", "wss://nos.lol", "wss://relay.damus.io"];
				Uri[] uris = relayUrls.Select(x => new Uri(x)).ToArray();
				_nostrClient = CreateNostrClient(uris, _torEndpoint);
				_nostrClient.EventsReceived += OnNostrEventsReceived;

				await _nostrClient.ConnectAndWaitUntilConnected(cancel).ConfigureAwait(false);

				_nostrSubscriptionID = Guid.NewGuid().ToString();
				await _nostrClient.CreateSubscription(_nostrSubscriptionID, [new() { Kinds = [1], Authors = [defaultPubKeyHex] }], cancel).ConfigureAwait(false);

			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				_nostrClient?.Dispose();
			}
		}
	}

	public static INostrClient CreateNostrClient(Uri[] relays, EndPoint? torEndpoint = null)
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
}
