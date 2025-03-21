using NNostr.Client;
using NNostr.Client.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Discoverability;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients;
public class WasabiNostrClient
{
	private const string DefaultPublicKey = "npub1l0p8r79n24ez6ahh93utyyu268hj7cg3gdsql4526rwlc6qhxx3sxy0yeu"; // Change this to Official Wasabi Nostr PubKey
	private EndPoint _torEndpoint;
	private string? _nostrSubscriptionID;

	public WasabiNostrClient(EndPoint endPoint)
	{
		_torEndpoint = endPoint;
	}

	public INostrClient? NostrWebClient { get; set; }
	private Dictionary<string, NostrEvent> Events { get; } = new();
	private object EventsLock { get; } = new();

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId == _nostrSubscriptionID)
		{
			lock (EventsLock)
			{
				foreach (NostrEvent nostrEvent in args.events)
				{
					var version = nostrEvent.Tags.FirstOrDefault(x => x.TagIdentifier == "Version")?.Data.FirstOrDefault();
					var downloadLink = nostrEvent.Tags.FirstOrDefault(x => x.TagIdentifier == "DownloadLink")?.Data.FirstOrDefault();

					if (version is not null && downloadLink is not null)
					{
						Events.TryAdd(nostrEvent.Id, nostrEvent);
					}
				}
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

				string[] relayUrls = ["wss://relay.primal.net", "wss://nos.lol", "wss://relay.damus.io"];
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

	public NostrUpdateInfo? GetLatestUpdateInfo()
	{
		lock (EventsLock)
		{
			var latestEvent = Events.Values.OrderByDescending(ev => ev.CreatedAt).FirstOrDefault();

			if (latestEvent is null)
			{
				return null;
			}

			// Nostr events must have these two tags in order to save them in Events property.
			string version = latestEvent.Tags.First(x => x.TagIdentifier == "Version").Data.First();
			string downloadLink = latestEvent.Tags.First(x => x.TagIdentifier == "DownloadLink").Data.First();

			return new NostrUpdateInfo(new Version(version), downloadLink);
		}
	}

	public record NostrUpdateInfo(Version Version, string DownloadLink);
}
