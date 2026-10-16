using Nostra.CSharp;
using static Nostra.Client;
using RelayClient = Nostra.Client.RelayClient;
using SubscriptionFilter = Nostra.Client.SubscriptionFilter;

namespace WalletWasabi.WebClients;

/// <summary>
/// Abstraction over Nostra's RelayClient to support both single and multi-relay scenarios.
/// </summary>
public interface INostrClient : IDisposable
{
	Task ConnectAsync(CancellationToken cancellationToken);
	Task DisconnectAsync(CancellationToken cancellationToken);
	void Subscribe(string subscriptionId, SubscriptionFilter filter);
	void Publish(Event signedEvent);
	Task StartListeningAsync(Action<object, RelayMessageResult> onMessage, Action<string>? onError, CancellationToken cancellationToken);
}

/// <summary>
/// Manages multiple relay connections in parallel.
/// </summary>
public class CompositeNostrClient : INostrClient
{
	private readonly Uri[] _relayUris;
	private readonly List<(Uri Uri, RelayClient Client)> _connectedClients = new();

	public int ConnectedCount => _connectedClients.Count;

	public CompositeNostrClient(Uri[] relays)
	{
		if (relays.Length == 0)
		{
			throw new ArgumentException("At least one relay is required.", nameof(relays));
		}
		_relayUris = relays;
	}

	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		var tasks = _relayUris.Select(async uri =>
		{
			try
			{
				var client = await ConnectToRelayAsync(uri).ConfigureAwait(false);
				return Result<(Uri, RelayClient), Exception>.Ok((uri, client));
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Connect failed for relay {uri}: {ex.Message}");
				return Result<(Uri, RelayClient), Exception>.Fail(ex);
			}
		});

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		foreach (var result in results.Where(r => r.IsOk))
		{
			_connectedClients.Add(result.Value);
		}

		var successCount = _connectedClients.Count;
		var failureCount = results.Count(r => !r.IsOk);

		if (failureCount > 0)
		{
			Logger.LogInfo($"Connect: {successCount}/{_relayUris.Length} relays succeeded");
		}

		if (successCount == 0)
		{
			throw new AggregateException(
				$"All {_relayUris.Length} Nostr relays failed during connection",
				results.Where(r => !r.IsOk).Select(r => r.Error));
		}
	}

	public Task DisconnectAsync(CancellationToken cancellationToken)
	{
		var tasks = _connectedClients.Select(c => c.Client.DisconnectAsync(cancellationToken));
		return Task.WhenAll(tasks);
	}

	public void Subscribe(string subscriptionId, SubscriptionFilter filter)
	{
		foreach (var (_, client) in _connectedClients)
		{
			client.Subscribe(subscriptionId, filter);
		}
	}

	public void Publish(Event signedEvent)
	{
		foreach (var (_, client) in _connectedClients)
		{
			client.Publish(signedEvent);
		}
	}

	public Task StartListeningAsync(Action<object, RelayMessageResult> onMessage, Action<string>? onError, CancellationToken cancellationToken)
	{
		var tasks = _connectedClients.Select(c =>
			c.Client.StartListeningAsync(
				msg => onMessage(c.Client, msg),
				onError));

		return Task.WhenAll(tasks);
	}

	public void Dispose()
	{
		// RelayClient doesn't implement IDisposable
		_connectedClients.Clear();
	}
}
