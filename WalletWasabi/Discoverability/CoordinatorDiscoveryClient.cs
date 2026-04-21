using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NNostr.Client;
using WalletWasabi.Logging;

namespace WalletWasabi.Discoverability;

public static class CoordinatorDiscoveryClient
{
	private const int CoordinatorAnnouncementKind = 15750;

	public static async Task<IReadOnlyList<DiscoveredCoordinator>> FetchAsync(
		INostrClient client,
		Network network,
		TimeSpan collectionWindow,
		CancellationToken cancellationToken)
	{
		var subscriptionId = Guid.NewGuid().ToString();
		var latestByPubKey = new Dictionary<string, NostrEvent>();
		var gate = new object();

		void OnEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
		{
			if (args.subscriptionId != subscriptionId)
			{
				return;
			}

			lock (gate)
			{
				foreach (var nostrEvent in args.events)
				{
					if (nostrEvent.Kind != CoordinatorAnnouncementKind || string.IsNullOrEmpty(nostrEvent.PublicKey))
					{
						continue;
					}

					if (!latestByPubKey.TryGetValue(nostrEvent.PublicKey, out var existing) ||
						(nostrEvent.CreatedAt ?? DateTimeOffset.MinValue) > (existing.CreatedAt ?? DateTimeOffset.MinValue))
					{
						latestByPubKey[nostrEvent.PublicKey] = nostrEvent;
					}
				}
			}
		}

		client.EventsReceived += OnEventsReceived;
		try
		{
			await client.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);
			var filter = new NostrSubscriptionFilter { Kinds = [CoordinatorAnnouncementKind] };
			await client.CreateSubscription(subscriptionId, [filter], cancellationToken).ConfigureAwait(false);
			await Task.Delay(collectionWindow, cancellationToken).ConfigureAwait(false);
			await client.CloseSubscription(subscriptionId, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			client.EventsReceived -= OnEventsReceived;
			try
			{
				await client.Disconnect().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Failed to disconnect Nostr client: {ex.Message}");
			}
		}

		return latestByPubKey
			.Select(x => TryParse(x.Key, x.Value, network))
			.OfType<DiscoveredCoordinator>()
			.ToList();
	}

	private static DiscoveredCoordinator? TryParse(string pubKey, NostrEvent evt, Network network)
	{
		string? Tag(string id) => evt.Tags.FirstOrDefault(t => t.TagIdentifier == id)?.Data.FirstOrDefault();

		if (Tag("type") != "wabisabi")
		{
			return null;
		}

		if (Network.GetNetwork(Tag("network") ?? "") != network)
		{
			return null;
		}

		if (!Uri.TryCreate(Tag("endpoint"), UriKind.Absolute, out var endpoint))
		{
			return null;
		}

		return new DiscoveredCoordinator(
			pubKey,
			Tag("name") ?? endpoint.Host,
			evt.Content ?? "",
			endpoint,
			evt.CreatedAt ?? DateTimeOffset.MinValue);
	}
}
