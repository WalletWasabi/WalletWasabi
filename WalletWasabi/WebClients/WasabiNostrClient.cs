using NNostr.Client;
using NNostr.Client.Protocols;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients;

public class WasabiNostrClient : IDisposable
{
	private readonly Channel<ReleaseInfo> _updateChannel = Channel.CreateUnbounded<ReleaseInfo>();
	private readonly Dictionary<string, NostrEvent> _events = new();
	private readonly HashSet<object> _eoseReceivedFrom = new();
	private readonly INostrClient _nostrClient;
	private readonly string _nostrSubscriptionId = Guid.NewGuid().ToString();
	private int _connectedClientsCount;

	public WasabiNostrClient(INostrClient nostrClient)
	{
		_nostrClient = nostrClient;
		_nostrClient.EventsReceived += OnNostrEventsReceived;
		_nostrClient.EoseReceived += OnEoseReceived;
	}

	public ChannelReader<ReleaseInfo> EventsReader => _updateChannel.Reader;

	public async Task ConnectAndSubscribeAsync(CancellationToken cancel)
	{
		await _nostrClient.ConnectAndWaitUntilConnected(cancel).ConfigureAwait(false);

		_connectedClientsCount = _nostrClient is CompositeNostrClient composite
			? composite.States.Count(s => s.Value is WebSocketState.Open)
			: 1;

		var nostrSubscriptionFilter = new NostrSubscriptionFilter {
			Kinds = [1],
			Authors = [NIP19.FromNIP19Npub(Constants.WasabiTeamNostrPubKey).ToHex()],
			Limit = 1};

		await _nostrClient.CreateSubscription(_nostrSubscriptionId, [nostrSubscriptionFilter], cancel).ConfigureAwait(false);
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		await _nostrClient.Disconnect().ConfigureAwait(false);
	}

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId != _nostrSubscriptionId)
		{
			return;
		}

		foreach (var nostrEvent in args.events)
		{
			if (!_events.TryAdd(nostrEvent.Id, nostrEvent))
			{
				continue;
			}

			try
			{
				var tags = nostrEvent.Tags.ToImmutableDictionary(t => t.TagIdentifier, t => t.Data.First());
				var releaseInfo = new ReleaseInfo(
					Version.Parse(tags["version"]),
					tags.Remove("version").ToImmutableDictionary(t => t.Key, t => new Uri(t.Value)));

				_updateChannel.Writer.TryWrite(releaseInfo);
			}
			catch (Exception)
			{
				Logger.LogError($"Invalid Nostr Event received. ID: {nostrEvent.Id}");
			}
		}
	}

	private void OnEoseReceived(object? sender, string subscriptionId)
	{
		if (subscriptionId != _nostrSubscriptionId || sender is null)
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
		_nostrClient.EventsReceived -= OnNostrEventsReceived;
		_nostrClient.EoseReceived -= OnEoseReceived;
	}
}

public record ReleaseInfo(Version Version, ImmutableDictionary<string, Uri> Assets);
