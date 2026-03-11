using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NNostr.Client;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.WebClients;

public class CompositeNostrClient : INostrClient
{
	private readonly NostrClient[] _clients;

	public Dictionary<Uri, WebSocketState?> States => _clients.ToDictionary(c => c.Relay, c => c.State);

	public CompositeNostrClient(Uri[] relays, Action<WebSocket> websocketConfigure)
	{
		_clients = relays.Select(r =>
		{
			var c = new NostrClient(r, websocketConfigure);
			c.MessageReceived += (sender, message) => MessageReceived?.Invoke(sender, message);
			c.InvalidMessageReceived += (sender, message) => InvalidMessageReceived?.Invoke(sender, message);
			c.NoticeReceived += (sender, message) => NoticeReceived?.Invoke(sender, message);
			c.EventsReceived += (sender, events) => EventsReceived?.Invoke(sender, events);
			c.OkReceived += (sender, ok) => OkReceived?.Invoke(sender, ok);
			c.EoseReceived += (sender, message) => EoseReceived?.Invoke(sender, message);
			c.StateChanged += (sender, state) => StateChanged?.Invoke(sender, (r, state));
			return c;
		}).ToArray();
	}

	public async Task Connect(CancellationToken token)
	{
		var tasks = _clients.Select(async client =>
		{
			try
			{
				await client.Connect(token).ConfigureAwait(false);
				return Result<NostrClient, Exception>.Ok(client);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Connect failed for relay {client.Relay}: {ex.Message}");
				return Result<NostrClient, Exception>.Fail(ex);
			}
		});

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = results.Count(r => r.IsOk);
		var failureCount = results.Count(r => !r.IsOk);

		if (failureCount > 0)
		{
			Logger.LogInfo($"Connect: {successCount}/{_clients.Length} relays succeeded");
		}

		if (successCount == 0)
		{
			throw new AggregateException(
				$"All {_clients.Length} Nostr relays failed during {"Connect"}",
				results.Where(r => !r.IsOk).Select(r => r.Error));
		}
	}

	public Task Disconnect() =>
		Task.WhenAll(_clients.Select(c => c.Disconnect()));

	public IAsyncEnumerable<string> ListenForRawMessages() =>
		_clients.Select(c => c.ListenForRawMessages()).ToArray().Merge();

	public Task ListenForMessages() =>
		Task.WhenAll(_clients.Select(c => c.ListenForMessages()));

	public Task PublishEvent(NostrEvent nostrEvent, CancellationToken token) =>
		Task.WhenAll(_clients.Select(c => c.PublishEvent(nostrEvent, token)));

	public Task CloseSubscription(string subscriptionId, CancellationToken token) =>
		Task.WhenAll(_clients.Select(c => c.CloseSubscription(subscriptionId, token)));

	public Task CreateSubscription(string subscriptionId, NostrSubscriptionFilter[] filters, CancellationToken token) =>
		Task.WhenAll(_clients.Select(c => c.CreateSubscription(subscriptionId, filters, token)));

	public void Dispose()
	{
		foreach (var client in _clients)
		{
			client.Dispose();
		}
	}

	public async Task ConnectAndWaitUntilConnected(CancellationToken connectionCancellationToken,
		CancellationToken lifetimeCancellationToken)
	{
		var tasks = _clients.Select(async client =>
		{
			try
			{
				await client.ConnectAndWaitUntilConnected(connectionCancellationToken, lifetimeCancellationToken).ConfigureAwait(false);
				return Result<NostrClient, Exception>.Ok(client);
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Connect failed for relay {client.Relay}: {ex.Message}");
				return Result<NostrClient, Exception>.Fail(ex);
			}
		});

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		var successCount = results.Count(r => r.IsOk);
		var failureCount = results.Count(r => !r.IsOk);

		if (failureCount > 0)
		{
			Logger.LogInfo($"Connect: {successCount} out of {_clients.Length} relays succeeded");
		}

		if (successCount == 0)
		{
			throw new AggregateException(
				$"All {_clients.Length} Nostr relays failed during connection",
				results.Where(r => !r.IsOk).Select(r => r.Error));
		}
	}

	public event EventHandler<string>? MessageReceived;
	public event EventHandler<string>? InvalidMessageReceived;
	public event EventHandler<string>? NoticeReceived;
	public event EventHandler<(string subscriptionId, NostrEvent[] events)>? EventsReceived;
	public event EventHandler<(string eventId, bool success, string messafe)>? OkReceived;
	public event EventHandler<string>? EoseReceived;
	public event EventHandler<(Uri, WebSocketState?)>? StateChanged;
}
