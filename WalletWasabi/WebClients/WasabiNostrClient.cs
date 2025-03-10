using NNostr.Client;
using NNostr.Client.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Discoverability;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients;
public class WasabiNostrClient
{
	private const string DefaultPublicKey = "npub1l0p8r79n24ez6ahh93utyyu268hj7cg3gdsql4526rwlc6qhxx3sxy0yeu";
	private EndPoint _torEndpoint;
	private string? _nostrSubscriptionID;

	public WasabiNostrClient(EndPoint endPoint)
	{
		_torEndpoint = endPoint;
		NostrUpdateChannel = Channel.CreateUnbounded<NostrUpdateInfo>();
	}

	public INostrClient? NostrWebClient { get; set; }
	public Channel<NostrUpdateInfo> NostrUpdateChannel { get; set; }

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId == _nostrSubscriptionID)
		{
			Version? newVersion = null;
			string? downloadLink = null;

			foreach (NostrEvent nostrEvents in args.events)
			{
				Logger.LogInfo(nostrEvents.Id);
				Logger.LogInfo("Content: " + nostrEvents.Content);
				Logger.LogInfo("Kind: " + nostrEvents.Kind.ToString());
				Logger.LogInfo("Created at: " + nostrEvents.CreatedAt.ToString());

				foreach (var eventTag in nostrEvents.Tags)
				{
					if (eventTag.TagIdentifier == "Version")
					{
						Logger.LogInfo("Version: " + eventTag.Data.First());
						Version version = new(eventTag.Data.First());
						newVersion = version;
					}

					if (eventTag.TagIdentifier == "DownloadLink")
					{
						Logger.LogInfo("DownloadLink: " + eventTag.Data.First());
						downloadLink = eventTag.Data.First();
					}
				}
			}

			if (newVersion is not null && downloadLink is not null)
			{
				NostrUpdateChannel.Writer.TryWrite(new NostrUpdateInfo(newVersion, downloadLink));
			}
		}
	}

	public async Task CheckNostrConnectionAsync(CancellationToken cancel)
	{
		if (NostrWebClient is null)
		{
			try
			{
				string defaultPubKeyHex = NIP19.FromNIP19Npub(DefaultPublicKey).ToHex();

				string[] relayUrls = ["wss://relay.primal.net"];
				Uri[] uris = relayUrls.Select(x => new Uri(x)).ToArray();
				NostrWebClient = NostrClientFactory.Create(uris, _torEndpoint);
				NostrWebClient.EventsReceived += OnNostrEventsReceived;

				await NostrWebClient.ConnectAndWaitUntilConnected(cancel).ConfigureAwait(false);

				_nostrSubscriptionID = Guid.NewGuid().ToString();
				await NostrWebClient.CreateSubscription(_nostrSubscriptionID, [new() { Kinds = [1], Authors = [defaultPubKeyHex] }], cancel).ConfigureAwait(false);

			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				NostrWebClient?.Dispose();
			}
		}
	}
	public record NostrUpdateInfo(Version Version, string DownloadLink);
}
