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
	private readonly INostrClient _nostrClient;
	private string _nostrSubscriptionID;

	public WasabiNostrClient(INostrClient nostrClient)
	{
		_nostrClient = nostrClient;
		_nostrClient.EventsReceived += OnNostrEventsReceived;
	}

	public ChannelReader<ReleaseInfo> EventsReader => _updateChannel.Reader;

	public async Task ConnectAnsSubscribeAsync(CancellationToken cancel)
	{
		await _nostrClient.ConnectAndWaitUntilConnected(cancel).ConfigureAwait(false);

		var nostrSubscriptionFilter = new NostrSubscriptionFilter {
			Kinds = [1],
			Authors = [NIP19.FromNIP19Npub(Constants.WasabiTeamNostrPubKey).ToHex()],
			Limit = 1};

		_nostrSubscriptionID = Guid.NewGuid().ToString();
		await _nostrClient.CreateSubscription(_nostrSubscriptionID, [nostrSubscriptionFilter], cancel).ConfigureAwait(false);
	}

	public async Task DisconnectAsync(CancellationToken cancellationToken)
	{
		await _nostrClient.Disconnect().ConfigureAwait(false);
	}

	private void OnNostrEventsReceived(object? sender, (string subscriptionId, NostrEvent[] events) args)
	{
		if (args.subscriptionId != _nostrSubscriptionID)
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

		CheckAllClientsFinished((INostrClient)sender!);
	}

	private void CheckAllClientsFinished(INostrClient individualClient)
	{
		individualClient.Disconnect();

		if (_nostrClient is CompositeNostrClient multiClient)
		{
			if (multiClient.States.All(s => s.Value is WebSocketState.Closed or WebSocketState.Aborted))
			{
				_updateChannel.Writer.TryComplete();
			}
		}
		else
		{
			_updateChannel.Writer.TryComplete();
		}
	}

	public void Dispose()
	{
		_nostrClient.EventsReceived -= OnNostrEventsReceived;
	}
}

public record ReleaseInfo(Version Version, ImmutableDictionary<string, Uri> Assets);
