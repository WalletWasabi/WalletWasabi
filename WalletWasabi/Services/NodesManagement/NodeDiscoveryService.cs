using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Services.NodesManagement;

public class NodeDiscoveryService : IDisposable
{
	abstract record CoordinatorMessage;
	record HarvestedEndpointsMessage(EndPoint[] Endpoints) : CoordinatorMessage;
	record PeerDiscoveredMessage(PeerInfo PeerInfo) : CoordinatorMessage;
	record PeerFailedMessage(EndPoint Endpoint) : CoordinatorMessage;
	record PeerQuickDisconnectMessage(EndPoint Endpoint) : CoordinatorMessage;
	record GetPeersMessage(IReplyChannel<PeerInfo[]> ReplyChannel) : CoordinatorMessage;
	record SlowDownCrawlingMessage : CoordinatorMessage;
	record SpeedUpCrawlingMessage : CoordinatorMessage;

	abstract record CrawlerMessage;

	record CrawlMessage(EndPoint EndPoint) : CrawlerMessage;
	record SlowDownMessage : CrawlerMessage;
	record SpeedUpMessage : CrawlerMessage;

	record CrawlingState(
		ImmutableDictionary<EndPoint, PeerInfo> Peers,
		int LastCrawlerIndex);

	private readonly TimeSpan _connectionTimeout;
	private readonly TimeSpan _harvestTimeout;

	private MailboxProcessor<CoordinatorMessage> _coordinator;
	private MailboxProcessor<CrawlerMessage>[] _crawlers;
	private readonly CancellationTokenSource _cts = new();
	private readonly Network _network;
	private readonly EndPoint? _torSocks5;
	private readonly int _maxConcurrency;

	private static int PriorityOf(CrawlerMessage m) => m switch
	{
		SlowDownMessage => 0,  // highest priority (smallest)
		SpeedUpMessage  => 1,
		_               => 2,  // lowest priority
	};

	private readonly Comparer<CrawlerMessage> _crawlerMessagePriority =
		Comparer<CrawlerMessage>.Create((a, b) => PriorityOf(a).CompareTo(PriorityOf(b)));

	public NodeDiscoveryService(Network network,
		EndPoint? torSocks5 = null,
		int maxConcurrency = 15,
		TimeSpan? connectionTimeout = null,
		TimeSpan? harvestTimeout = null)
	{
		_network = network;
		_torSocks5 = torSocks5;
		_maxConcurrency = maxConcurrency;
		_connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(10);
		_harvestTimeout = harvestTimeout ?? TimeSpan.FromSeconds(3);

		_coordinator = Coordinator(_cts.Token);
		_crawlers = Enumerable
			.Range(0, maxConcurrency)
			.Select(n => ProbeWorkerAsync($"crawler-{n}", _cts.Token))
			.ToArray();

		MailboxProcessor<CrawlerMessage> ProbeWorkerAsync(string workerName, CancellationToken cancellationToken) =>
			Spawn(workerName, EventDriven<CrawlerMessage, TimeSpan>(TimeSpan.Zero, ProbeAsync),
				comparer: _crawlerMessagePriority,
				cancellationToken);

		MailboxProcessor<CoordinatorMessage> Coordinator(CancellationToken cancellationToken) =>
			Spawn("crawler-coordinator",
				EventDriven<CoordinatorMessage, CrawlingState>(new CrawlingState([], 0), BalanceCrawlingAsync),
				cancellationToken);
	}

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		Logger.LogInfo("Starting node discovery service.");

		await SeedFromDnsAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task StopAsync()
	{
		Logger.LogInfo("Stopping node discovery service.");

		await _cts.CancelAsync().ConfigureAwait(false);

		Logger.LogInfo("Node discovery service stopped.");
	}

	public Network Network => _network;

	public void SlowDownCrawling() =>
		_coordinator.Post(new SlowDownCrawlingMessage());

	public void SpeedUpCrawling() =>
		_coordinator.Post(new SpeedUpCrawlingMessage());

	public void ReportQuickDisconnect(EndPoint endpoint) =>
		_coordinator.Post(new PeerQuickDisconnectMessage(endpoint));

	public async Task<PeerInfo[]> GetKnownPeersAsync(CancellationToken cancellationToken = default)
	{
		return await _coordinator.PostAndReplyAsync<PeerInfo[]>(
			reply => new GetPeersMessage(reply),
			cancellationToken).ConfigureAwait(false);
	}

	private Task<CrawlingState> BalanceCrawlingAsync(CoordinatorMessage msg, CrawlingState state, CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case HarvestedEndpointsMessage (Endpoints: var endpoints):
				var n = state.LastCrawlerIndex;
				foreach (var endPoint in endpoints)
				{
					var wi = n % _maxConcurrency;
					_crawlers[wi].Post(new CrawlMessage(endPoint));
					n++;
				}

				state = state with {LastCrawlerIndex = n};
				break;

			case PeerDiscoveredMessage (PeerInfo: var peer):
				var updatedPeer = state.Peers.TryGetValue(peer.Endpoint, out var existingPeer)
					? existingPeer with { LastSeen = DateTimeOffset.UtcNow, SuccessfulProbes = existingPeer.SuccessfulProbes + 1}
					: peer;

				state = state with {Peers = state.Peers.SetItem(peer.Endpoint, updatedPeer)};
				break;

			case PeerFailedMessage (Endpoint: var endpoint):
				if (state.Peers.TryGetValue(endpoint, out var failingPeer))
				{
					var failures = failingPeer.FailedProbes + 1;
					state = failures > failingPeer.SuccessfulProbes * 2 && failures >= 3
						? state with {Peers = state.Peers.Remove(endpoint)}
						: state with {Peers = state.Peers.SetItem(endpoint, failingPeer with {FailedProbes = failures})};
				}
				break;

			case PeerQuickDisconnectMessage (Endpoint: var disconnectedEndpoint):
				if (state.Peers.TryGetValue(disconnectedEndpoint, out var unstablePeer))
				{
					var quickDisconnects = unstablePeer.QuickDisconnects + 1;
					state = quickDisconnects >= 3
						? state with {Peers = state.Peers.Remove(disconnectedEndpoint)}
						: state with {Peers = state.Peers.SetItem(disconnectedEndpoint, unstablePeer with {QuickDisconnects = quickDisconnects})};
				}
				break;

			case GetPeersMessage (ReplyChannel: var replyChannel):
				replyChannel.Reply(state.Peers.Values.ToArray());
				break;

			case SlowDownCrawlingMessage:
				var slowDownMessage = new SlowDownMessage();
				foreach (var crawler in _crawlers)
				{
					crawler.Post(slowDownMessage);
				}
				break;

			case SpeedUpCrawlingMessage:
				var speedUpMessage = new SpeedUpMessage();
				foreach (var crawler in _crawlers)
				{
					crawler.Post(speedUpMessage);
				}
				break;

		}

		return Task.FromResult(state);
	}

	private async Task<TimeSpan> ProbeAsync(CrawlerMessage msg, TimeSpan delay, CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case CrawlMessage (var endpoint):
				await CrawlEndpointAsync(endpoint, delay, cancellationToken).ConfigureAwait(false);
				break;
			case SlowDownMessage:
				delay += TimeSpan.FromSeconds(1);
				break;
			case SpeedUpMessage:
				delay = TimeSpan.Zero;
				break;
		}

		return delay;
	}

	private async Task CrawlEndpointAsync(EndPoint endpoint, TimeSpan delay, CancellationToken cancellationToken)
	{
		await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(_connectionTimeout);

		var connParams = new NodeConnectionParameters
		{
			ConnectCancellation = timeoutCts.Token,
			IsRelay = false,
			UserAgent = Constants.UserAgents[Random.Shared.Next(Constants.UserAgents.Length)]
		};

		if (endpoint.IsTor())
		{
			if (_torSocks5 is null)
			{
				return;
			}

			connParams.TemplateBehaviors.Add(new SocksSettingsBehavior(_torSocks5));
		}

		Node? node = null;
		var sw = Stopwatch.StartNew();
		try
		{
			node = await Node.ConnectAsync(_network, endpoint, connParams).ConfigureAwait(false);
			node.VersionHandshake(timeoutCts.Token);

			sw.Stop();

			if (node.State != NodeState.HandShaked)
			{
				NotifyCoordinator(new PeerFailedMessage(endpoint));
				return;
			}

			var now = DateTimeOffset.UtcNow;
			var pv = node.PeerVersion;
			var peer = new PeerInfo
			{
				Endpoint = endpoint,
				UserAgent = pv.UserAgent ?? "Unknown",
				ProtocolVersion = pv.Version,
				Services = pv.Services,
				StartHeight = pv.StartHeight,
				ConnectionTime = sw.Elapsed,
				DiscoveredAt = now,
				LastSeen = now
			};

			NotifyCoordinator(new PeerDiscoveredMessage(peer));

			await HarvestAddressesAsync(node, timeoutCts.Token).ConfigureAwait(false);
		}
		catch
		{
			NotifyCoordinator(new PeerFailedMessage(endpoint));
		}
		finally
		{
			node?.DisconnectAsync();
		}
	}

	private async Task HarvestAddressesAsync(Node node, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<EndPoint[]>(TaskCreationOptions.RunContinuationsAsynchronously);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(_harvestTimeout);
		var token = timeoutCts.Token;
		using var _ = token.Register(() => tcs.TrySetCanceled(token));

		node.MessageReceived += OnMessage;
		try
		{
			await node.SendMessageAsync(new GetAddrPayload()).ConfigureAwait(false);
			var harvestedEndpoints = await tcs.Task.ConfigureAwait(false);
			NotifyCoordinator(new HarvestedEndpointsMessage(harvestedEndpoints));
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
		}
		finally
		{
			node.MessageReceived -= OnMessage;
		}

		return;

		void OnMessage(object? _, IncomingMessage e)
		{
			if (e.Message.Payload is not AddrPayload addr)
			{
				return;
			}

			var harvested = new List<EndPoint>();
			foreach (var a in addr.Addresses)
			{
				if (a.Endpoint is { } ep)
				{
					harvested.Add(ep);
				}
			}

			tcs.TrySetResult(harvested.ToArray());
		}
	}

	private async Task SeedFromDnsAsync(CancellationToken cancellationToken)
	{
		Logger.LogInfo("Seeding from DNS...");

		async Task<Result<IPAddress[], Exception>> GetAddressesFromDnsAsync(string dnsServerHost)
		{
			try
			{
				return await Dns.GetHostAddressesAsync(dnsServerHost, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				return e;
			}
		}

		var tasks = _network.DNSSeeds
			.Select(x => x.Host)
			.Select(GetAddressesFromDnsAsync);

		await foreach (var task in Task.WhenEach(tasks).WithCancellation(cancellationToken))
		{
			var dnsQueryResult = await task.ConfigureAwait(false);

			if (dnsQueryResult.IsOk)
			{
				var endpoints = dnsQueryResult.Value
					.Select(x => new IPEndPoint(x, _network.DefaultPort))
					.Cast<EndPoint>()
					.ToArray();
				NotifyCoordinator(new HarvestedEndpointsMessage(endpoints));
			}
		}

		var endpointsFromSeedNodes = _network.SeedNodes
			.Select(x => x.Endpoint)
			.ToArray();

		NotifyCoordinator(new HarvestedEndpointsMessage(endpointsFromSeedNodes));
	}

	private void NotifyCoordinator(CoordinatorMessage msg) =>
		Tell("crawler-coordinator", msg);

	public void Dispose()
	{
		_cts.Cancel();

		_coordinator.Dispose();
		foreach (var crawler in _crawlers)
		{
			crawler.Dispose();
		}

		_cts.Dispose();
	}
}
