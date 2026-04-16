using System.Collections.Generic;
using System.Collections.Immutable;
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

	private static bool NetworkMatches(string eventNetwork, Network clientNetwork)
	{
		var tag = eventNetwork.Trim().ToLowerInvariant();
		if (clientNetwork == Network.Main)
		{
			return tag is "main" or "mainnet";
		}
		if (clientNetwork == Network.TestNet)
		{
			return tag is "test" or "testnet" or "testnet4";
		}
		if (clientNetwork == Network.RegTest)
		{
			return tag is "reg" or "regtest";
		}
		return string.Equals(tag, clientNetwork.ChainName.ToString(), StringComparison.OrdinalIgnoreCase);
	}

	public static async Task<IReadOnlyList<DiscoveredCoordinator>> FetchAsync(
		INostrClient client,
		Network network,
		TimeSpan collectionWindow,
		CancellationToken cancellationToken)
	{
		var subscriptionId = Guid.NewGuid().ToString();
		var latestByPubKey = new Dictionary<string, NostrEvent>();
		var relaysByPubKey = new Dictionary<string, HashSet<Uri>>();
		var lockObj = new object();

		void OnEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
		{
			if (args.subscriptionId != subscriptionId)
			{
				return;
			}

			var relayUri = (sender as NostrClient)?.Relay;

			lock (lockObj)
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

					if (relayUri is not null)
					{
						if (!relaysByPubKey.TryGetValue(nostrEvent.PublicKey, out var relays))
						{
							relays = new HashSet<Uri>();
							relaysByPubKey[nostrEvent.PublicKey] = relays;
						}
						relays.Add(relayUri);
					}
				}
			}
		}

		client.EventsReceived += OnEventsReceived;

		try
		{
			await client.ConnectAndWaitUntilConnected(cancellationToken).ConfigureAwait(false);

			var filter = new NostrSubscriptionFilter
			{
				Kinds = [CoordinatorAnnouncementKind]
			};

			await client.CreateSubscription(subscriptionId, [filter], cancellationToken).ConfigureAwait(false);

			using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			windowCts.CancelAfter(collectionWindow);

			try
			{
				await Task.Delay(collectionWindow, windowCts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// Collection window elapsed normally.
			}

			try
			{
				await client.CloseSubscription(subscriptionId, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Failed to close Nostr subscription: {ex.Message}");
			}
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

		var results = new List<DiscoveredCoordinator>();

		foreach (var (pubKey, evt) in latestByPubKey)
		{
			if (TryParse(pubKey, evt, network, relaysByPubKey.TryGetValue(pubKey, out var relays) ? relays.Count : 1, out var coordinator))
			{
				results.Add(coordinator);
			}
		}

		return results;
	}

	private static bool TryParse(string pubKey, NostrEvent evt, Network network, int relayCount, out DiscoveredCoordinator coordinator)
	{
		coordinator = null!;

		try
		{
			var tags = evt.Tags.ToImmutableDictionary(t => t.TagIdentifier ?? string.Empty, t => t.Data.FirstOrDefault() ?? string.Empty);

			if (!tags.TryGetValue("type", out var type) || type != "wabisabi")
			{
				return false;
			}

			if (!tags.TryGetValue("network", out var eventNetwork) || !NetworkMatches(eventNetwork, network))
			{
				return false;
			}

			if (!tags.TryGetValue("endpoint", out var endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var coordinatorUri))
			{
				return false;
			}

			var name = tags.TryGetValue("name", out var n) ? n : coordinatorUri.Host;

			var minInputCount = 0;
			if (tags.TryGetValue("absolutemininputcount", out var minInputCountStr) &&
				int.TryParse(minInputCountStr, out var parsed) &&
				parsed > 0)
			{
				minInputCount = parsed;
			}

			Uri? readMoreUri = null;
			if (tags.TryGetValue("readmore", out var readMore))
			{
				Uri.TryCreate(readMore, UriKind.Absolute, out readMoreUri);
			}

			var createdAt = evt.CreatedAt ?? DateTimeOffset.MinValue;

			coordinator = new DiscoveredCoordinator(
				pubKey,
				name,
				evt.Content ?? string.Empty,
				network,
				coordinatorUri,
				minInputCount,
				readMoreUri,
				createdAt,
				relayCount);

			return true;
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"Failed to parse coordinator announcement {evt.Id}: {ex.Message}");
			return false;
		}
	}
}
