using System.Collections.Concurrent;
using System.Threading.Channels;
using Nostra;
using Nostra.CSharp;

namespace WalletWasabi.WebClients;

public class WasabiNostrClient : IDisposable
{
	private readonly Channel<ReleaseInfo> _updateChannel = Channel.CreateUnbounded<ReleaseInfo>();
	private readonly ConcurrentDictionary<string, Event> _events = new();
	private readonly HashSet<object> _eoseReceivedFrom = new();
	private readonly INostrClient _nostrClient;
	private readonly AuthorIdT _pubkey;
	private readonly string _nostrSubscriptionId = Guid.NewGuid().ToString();
	private int _connectedClientsCount;
	private CancellationTokenSource? _listeningCts;

	public WasabiNostrClient(INostrClient nostrClient, string pubkeyNpub)
	{
		_nostrClient = nostrClient;
		_pubkey = Shareable.FromNPub(pubkeyNpub)
		          ?? throw new ArgumentException("The pubkey was not specified.", nameof(pubkeyNpub));
	}

	public ChannelReader<ReleaseInfo> EventsReader => _updateChannel.Reader;

	public async Task ConnectAndSubscribeAsync(CancellationToken cancel)
	{
		await _nostrClient.ConnectAsync(cancel).ConfigureAwait(false);

		_connectedClientsCount = _nostrClient is CompositeNostrClient composite
			? composite.ConnectedCount
			: 1;

		var author = Shareable.FromNPub(Constants.WasabiTeamNostrPubKey)
			?? throw new ArgumentException("The pubkey was not specified.", nameof(_pubkey));
		var filter = Filter.All
			.Notes()
			.ByAuthors(author)
			.Limit(1);

		_nostrClient.Subscribe(_nostrSubscriptionId, filter);

		_listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		_ = _nostrClient.StartListeningAsync(OnMessageReceived, OnError, _listeningCts.Token);
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		_listeningCts?.Cancel();
		await _nostrClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
	}

	private void OnMessageReceived(object sender, RelayMessageResult message)
	{
		switch (message)
		{
			case RelayMessageResult.Event evt:
				OnNostrEventReceived(sender, evt.SubscriptionId, evt.EventData);
				break;
			case RelayMessageResult.EndOfStoredEvents eose:
				OnEoseReceived(sender, eose.SubscriptionId);
				break;
		}
	}

	private void OnError(string error)
	{
		Logger.LogDebug($"Nostr error: {error}");
	}

	private void OnNostrEventReceived(object sender, string subscriptionId, EventT nostrEvent)
	{
		if (subscriptionId != _nostrSubscriptionId)
		{
			return;
		}

		if (!AuthorIds.equals(nostrEvent.PubKey, _pubkey))
		{
			return;
		}

		var eventIdHex = EventIds.ToHex(nostrEvent.Id);
		if (!_events.TryAdd(eventIdHex, nostrEvent))
		{
			return;
		}

		try
		{
			var tags = nostrEvent.Tags
				.Select(t => (Key: t.Item1, Value: t.Item2.FirstOrDefault() ?? ""))
				.ToImmutableDictionary(t => t.Key, t => t.Value);

			var releaseInfo = new ReleaseInfo(
				Version.Parse(tags["version"]),
				tags.Remove("version").ToImmutableDictionary(t => t.Key, t => new Uri(t.Value)));

			_updateChannel.Writer.TryWrite(releaseInfo);
		}
		catch (Exception)
		{
			Logger.LogError($"Invalid Nostr Event received. ID: {eventIdHex}");
		}
	}

	private void OnEoseReceived(object sender, string subscriptionId)
	{
		if (subscriptionId != _nostrSubscriptionId)
		{
			return;
		}

		lock (_eoseReceivedFrom)
		{
			_eoseReceivedFrom.Add(sender);

			if (_eoseReceivedFrom.Count >= _connectedClientsCount)
			{
				_updateChannel.Writer.TryComplete();
			}
		}
	}

	public void Dispose()
	{
		_listeningCts?.Dispose();
	}
}

public record ReleaseInfo(Version Version, ImmutableDictionary<string, Uri> Assets);
