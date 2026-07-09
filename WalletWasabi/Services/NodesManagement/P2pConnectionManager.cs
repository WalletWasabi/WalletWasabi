using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Services.NodesManagement;

public record P2pNodeGroupClient(
	Node[] Nodes,
	double Timeout,
	Action<int> IncreaseTimeout,
	Action<Node, P2pConnectionManager.MisbehaviorType> Error)
{
	private static readonly Meter Meter = new("P2pNodeGroupClient", "1.0.0");

	private static readonly Histogram<double> BlockDownloadDuration = Meter.CreateHistogram<double>("p2p.block.download.duration","ms", "Time taken to download and validate a block");
	private static readonly Counter<long> BlockDownloadAttempts = Meter.CreateCounter<long>("p2p.block.download.attempts", description: "Total number of block download attempts across all nodes");
	private static readonly Counter<long> BlockDownloadSuccesses = Meter.CreateCounter<long>("p2p.block.download.successes", description: "Number of successful block downloads");
	private static readonly Counter<long> BlockDownloadFailures = Meter.CreateCounter<long>("p2p.block.download.failures", description: "Number of failed block download attempts");

	/// <summary>
	/// Attempts to download the block from <see cref="Nodes"/> in parallel and return the first valid block it receives.
	/// </summary>
	public async Task<Block?> GetBlockAsync(uint256 blockHash, CancellationToken cancellationToken)
	{
		var blockHashStr = blockHash.ToString();
		BlockDownloadAttempts.Add(1, new KeyValuePair<string, object?>("block_hash", blockHashStr));

		Block? result = null;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Timeout));
		using var lts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

		// Map download tasks to their corresponding P2P nodes.
		var tasksToNodes = Nodes.ToDictionary(x => x.DownloadBlockAsync(blockHash, lts.Token), x => x);
		var tasks = tasksToNodes.Keys;
		var taskCount = tasks.Count;

		Stopwatch stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < taskCount; i++)
		{
			// Wait for a single download task to complete.
			var task = await Task.WhenAny(tasks).ConfigureAwait(false);
			var node = tasksToNodes[task];

			try
			{
				var block = await task.ConfigureAwait(false);

				// Validate block
				if (block.Check())
				{
					TrackSuccess(blockHashStr, stopwatch, node);
					IncreaseTimeout(-1);

					Logger.LogInfo($"Block ({block.GetCoinbaseHeight()}) downloaded: {block.GetHash()}.");
					result = block;
					break;
				}

				ReportError(blockHashStr, node, P2pConnectionManager.MisbehaviorType.ProvidedInvalidData);
			}
			catch (Exception ex)
			{
				if (ex is OperationCanceledException or TimeoutException)
				{
					IncreaseTimeout(+1);
					// It could be a slow connection and not a misbehaving node.
					ReportError(blockHashStr, node, P2pConnectionManager.MisbehaviorType.TimedOutDownloadingBlock, ex);

					// If the download was canceled due to timeout, we can break the loop and return null.
					break;
				}
				else
				{
					Logger.LogDebug(ex);
					ReportError(blockHashStr, node, P2pConnectionManager.MisbehaviorType.Unknown, ex);
				}
			}

			// Remove task to node mapping to avoid waiting for it again.
			tasksToNodes.Remove(task);
			tasks = tasksToNodes.Keys;
		}

		// Cancel all downloads if any.
		await lts.CancelAsync().ConfigureAwait(false);

		// Wait for all tasks to complete, ignoring exceptions.
		try
		{
			await Task.WhenAll(tasks).ConfigureAwait(false);
		}
		catch
		{
		}

		return result;

		static void TrackSuccess(string blockHashStr, Stopwatch stopwatch, Node node)
		{
			BlockDownloadDuration.Record(stopwatch.ElapsedMilliseconds,
				new("block_hash", blockHashStr), new("status", "success"), new("node", node.RemoteSocketEndpoint?.ToString() ?? "unknown"));
			BlockDownloadSuccesses.Add(1, new KeyValuePair<string, object?>("block_hash", blockHashStr));
		}

		void ReportError(string blockHashStr, Node node, P2pConnectionManager.MisbehaviorType misbehaviorType, Exception? ex = null)
		{
			Error(node, misbehaviorType);
			BlockDownloadFailures.Add(1, new("block_hash", blockHashStr), new("misbehavior", misbehaviorType), new("exception", ex?.GetType().FullName));
		}
	}
}

public delegate Task<P2pNodeGroupClient> P2pNodeProvider(CancellationToken cancellationToken);

/// <summary>Snapshot of currently connected P2P nodes.</summary>
public delegate ImmutableArray<Node> P2pNodeListProvider();

public class P2pConnectionManager : IDisposable
{
	private const int TargetConnections = 12;
	private const int MinCompactFilterNodes = 5;
	private const double RotationScoreThreshold = 1.1;
	private const int DefaultCrawlerCount = 10;

	private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan QuickDisconnectThreshold = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan MaintainInterval = TimeSpan.FromSeconds(6);
	private static readonly TimeSpan RotateInterval = TimeSpan.FromMinutes(3);
	private static readonly TimeSpan CrawlerConnectionTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan CrawlerHarvestTimeout = TimeSpan.FromSeconds(15);

	private readonly Network _network;
	private readonly List<NodeBehavior> _templateBehaviors = [];
	private readonly EventBus _eventBus;
	private readonly IDnsResolver _dnsResolver;
	private readonly TimeSpan _connectionTimeout;
	private readonly int _crawlerCount;
	private readonly EndPoint? _torSocks5;

	private readonly ConcurrentDictionary<EndPoint, (Node Node, PeerInfo PeerInfo, DateTimeOffset ConnectedAt)> _connectedNodes = new();
	private readonly ConcurrentDictionary<EndPoint, DateTimeOffset> _connectionAttempts = new();
	private readonly ComposedDisposable _disposables = new();

	private MailboxProcessor<CrawlerMessage>[]? _crawlers;
	private MailboxProcessor<CoordinatorMessage>? _discoveryCoordinator;

	private int _isReevaluating;
	private DateTimeOffset _lastMaintainTime;
	private DateTimeOffset _lastRotateTime;

	private int _timeoutsCounter;
	private int _currentTimeoutSeconds = 16;

	private bool _isDisposed;

	public P2pConnectionManager(
		Network network,
		EventBus eventBus,
		IDnsResolver dnsResolver,
		TimeSpan connectionTimeout,
		int crawlerCount = DefaultCrawlerCount,
		EndPoint? torSocks5 = null)
	{
		_network = network;
		_eventBus = eventBus;
		_dnsResolver = dnsResolver;
		_connectionTimeout = connectionTimeout;
		_crawlerCount = crawlerCount;
		_torSocks5 = torSocks5;
	}

	public ImmutableArray<Node> Nodes => _connectedNodes.Values.Select(x => x.Node).Where(x => x.IsConnected).ToImmutableArray();

	public void AddBehavior(NodeBehavior behavior)
	{
		_templateBehaviors.Add(behavior);
		foreach (var (_, node) in _connectedNodes)
		{
			node.Node.Behaviors.Add(behavior);
		}
	}

	public void Start(CancellationToken cancellationToken)
	{
		_crawlers = Enumerable
			.Range(0, _crawlerCount)
			.Select(n =>
				Spawn($"crawler-{n}",
					EventDriven(
						new CrawlerState(DelayBeforeVisitingNode: TimeSpan.Zero),
						CreateCrawler()),
					capacity: 1_000,
					cancellationToken: cancellationToken))
			.ToArray();

		_disposables.AddRange(_crawlers);

		_discoveryCoordinator = Spawn(
			"BitcoinP2pNodeDiscoveryServiceCoordinator",
			Service("Bitcoin Node Discovery Service",
				EventDriven(
					new CrawlingCoordinationState(SlowedDown: false, Peers: ImmutableDictionary<EndPoint, PeerInfo>.Empty, LastCrawlerIndex: 0),
					CreateDiscovery(_crawlers))),
			cancellationToken: cancellationToken);
		_discoveryCoordinator.DisposeUsing(_disposables);

		_ = Task.Run(() => SeedFromDnsAsync(cancellationToken), cancellationToken);

		_eventBus.Subscribe<Tick>(async void (_) =>
		{
			await ReevaluateConnectionsAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
		}).DisposeUsing(_disposables);

		_eventBus.Subscribe<NodeDisconnectedQuickly>(e =>
			ReportMisbehavior(e.EndPoint, MisbehaviorType.DisconnectedQuickly)).DisposeUsing(_disposables);

		_eventBus.Subscribe<MisbehavingNodeDetected>(e =>
			ReportMisbehavior(e.EndPoint, MisbehaviorType.ProvidedInvalidData)).DisposeUsing(_disposables);

		_eventBus.Subscribe<NodeTimeoutDownloadingBlock>(e =>
			ReportMisbehavior(e.EndPoint, MisbehaviorType.TimedOutDownloadingBlock)).DisposeUsing(_disposables);
	}

	public async Task ReevaluateConnectionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (Interlocked.CompareExchange(ref _isReevaluating, 1, 0) != 0)
		{
			return;
		}

		try
		{
			PurgeDisconnectedNodes();

			var count = _connectedNodes.Count;
			if ((now - _lastMaintainTime >= MaintainInterval && count < TargetConnections) || count == 0)
			{
				_lastMaintainTime = now;
				await ConnectToBestPeersAsync(cancellationToken).ConfigureAwait(false);
			}

			if ((now - _lastRotateTime >= RotateInterval && count > TargetConnections - 3) ||
			    (now - _lastRotateTime >= TimeSpan.FromSeconds(4) && _connectedNodes.Count(x => x.Value.PeerInfo.SupportsCompactFilters) < MinCompactFilterNodes))
			{
				_lastRotateTime = now;
				await RotateToBetterPeersAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception e)
		{
			Logger.LogWarning(e.Message);
		}
		finally
		{
			Interlocked.Exchange(ref _isReevaluating, 0);
		}
	}

	/// <seealso cref="P2pNodeProvider"/>
	public async Task<P2pNodeGroupClient> GetSingleUseNodeAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			var nodes = Nodes;

			if (nodes.Length == 0)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
				continue;
			}

			// Get random nodes from the connected nodes. The random sample contains 1 to 3 nodes.
			var randomNodes = nodes.ToShuffled(SecureRandom.Instance).Take(3).ToArray();

			return new P2pNodeGroupClient(
				randomNodes,
				GetCurrentTimeout(),
				UpdateTimeout,
				(node, misbehavior) =>
				{
					DisconnectNode(node, misbehavior);
					ReportMisbehavior(node.RemoteSocketEndpoint, misbehavior);
				});
		}

		cancellationToken.ThrowIfCancellationRequested();
		throw new InvalidOperationException("Failed to retrieve a connected node.");
	}

	public void DisconnectNode(Node node, MisbehaviorType misbehaviorType)
	{
		var shouldDisconnect = (misbehaviorType, Nodes.Length) switch
		{
			(MisbehaviorType.ProvidedInvalidData, _) => true,
			(MisbehaviorType.Unknown, _) => true,
			(MisbehaviorType.TimedOutDownloadingBlock, > 5) => true,
			(_, < 5) => false,
			(_, _) => node.SupportsCompactFilters,
		};

		if (!shouldDisconnect)
		{
			return;
		}

		var disconnectionReason = misbehaviorType switch
		{
			MisbehaviorType.ProvidedInvalidData => "Reason: it provided invalid data",
			MisbehaviorType.TimedOutDownloadingBlock => "Reason: it took too long to download a block",
			_ => ""
		};
		Logger.LogInfo($"Node {node.RemoteSocketEndpoint} disconnected. {disconnectionReason}");
		node.DisconnectAsync();
	}

	private double GetCurrentTimeout()
	{
		// More permissive timeout if few nodes are connected to avoid exhaustion.
		return Nodes.Length < 3
			? Math.Min(_currentTimeoutSeconds * 1.5, 600)
			: _currentTimeoutSeconds;
	}

	/// <summary>
	/// Current timeout used when downloading a block from the remote node. It is defined in seconds.
	/// </summary>
	private void UpdateTimeout(int addition)
	{
		_timeoutsCounter += addition;

		var timeout = _currentTimeoutSeconds;

		// If it times out 2 times in a row then increase the timeout.
		if (_timeoutsCounter >= 2)
		{
			_timeoutsCounter = 0;
			timeout = (int)Math.Round(timeout * 1.5);
		}
		else if (_timeoutsCounter <= -3) // If it does not time out 3 times in a row, lower the timeout.
		{
			_timeoutsCounter = 0;
			timeout = (int)Math.Round(timeout * 0.7);
		}

		// Sanity check
		var minTimeout = _network == Network.Main ? 3 : 2;

		if (timeout < minTimeout)
		{
			timeout = minTimeout;
		}
		else if (timeout > 600)
		{
			timeout = 600;
		}

		_currentTimeoutSeconds = timeout;
		Logger.LogInfo($"Current timeout value used on block download is: {timeout} seconds.");
	}

	private async Task<PeerInfo[]> GetPeersAsync(CancellationToken cancellationToken)
	{
		if (_discoveryCoordinator is null)
		{
			return [];
		}

		return await _discoveryCoordinator.PostAndReplyAsync<PeerInfo[]>(
			reply => new GetPeersMessage(reply),
			cancellationToken).ConfigureAwait(false);
	}

	private async Task ConnectToBestPeersAsync(CancellationToken cancellationToken)
	{
		var filterNodeCount = _connectedNodes.Values
			.Count(n => n.Node.IsConnected && n.PeerInfo.SupportsCompactFilters);

		var filterNodesNeeded = Math.Max(0, MinCompactFilterNodes - filterNodeCount);
		var totalNeeded = TargetConnections - _connectedNodes.Count;
		var availablePeers = await GetAvailablePeersAsync(cancellationToken).ConfigureAwait(false);

		var filterPeers = availablePeers
			.Where(p => p.SupportsCompactFilters)
			.OrderByDescending(p => p.Score)
			.Take(filterNodesNeeded * 2)
			.ToArray();

		var otherPeers = availablePeers.Except(filterPeers)
			.OrderByDescending(p => p.Score)
			.Take(totalNeeded * 2)
			.ToArray();

		var peers = filterPeers
			.Concat(otherPeers)
			.DistinctBy(p => p.Endpoint)
			.Take(totalNeeded)
			.Shuffle()
			.ToArray();

		if (peers.Length > 0)
		{
			Logger.LogTrace($"Connecting to {peers.Length} peers (current: {_connectedNodes.Count}).");
			await Task.WhenAll(peers.Select(p => ConnectToPeerAsync(p, cancellationToken))).ConfigureAwait(false);
		}
	}

	private async Task<PeerInfo[]> GetAvailablePeersAsync(CancellationToken cancellationToken)
	{
		var connectedKeys = _connectedNodes.Keys.ToHashSet();
		var now = DateTimeOffset.UtcNow;
		var cooldownEndpoints = _connectionAttempts
			.Where(kvp => now - kvp.Value < ReconnectCooldown)
			.Select(kvp => kvp.Key)
			.ToHashSet();

		var peers = await GetPeersAsync(cancellationToken).ConfigureAwait(false);
		var availablePeers = peers.Where(p => IsAvailable(p.Endpoint)).ToArray();
		return availablePeers;

		bool IsAvailable(EndPoint endpoint) =>
			!connectedKeys.Contains(endpoint) && !cooldownEndpoints.Contains(endpoint);
	}

	private async Task ConnectToPeerAsync(PeerInfo peerInfo, CancellationToken cancellationToken)
	{
		if (!TryReserveConnectionAttempt(peerInfo.Endpoint))
		{
			return;
		}

		try
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(_connectionTimeout);

			var connParams = new NodeConnectionParameters
			{
				ConnectCancellation = timeoutCts.Token,
				IsRelay = true,
				UserAgent = Constants.UserAgents[Random.Shared.Next(Constants.UserAgents.Length)]
			};

			foreach (var behavior in _templateBehaviors)
			{
				connParams.TemplateBehaviors.Add(behavior);
			}

			if (peerInfo.Endpoint.IsTor() && _torSocks5 is { } torEndpoint)
			{
				connParams.TemplateBehaviors.Add(new SocksSettingsBehavior(torEndpoint, onlyForOnionHosts: false,
					networkCredential: null, streamIsolation: true));
			}

			var node = await Node.ConnectAsync(_network, peerInfo.Endpoint, connParams)
				.ConfigureAwait(false);
			await node.VersionHandshakeAsync(timeoutCts.Token).ConfigureAwait(false);

			if (node.State != NodeState.HandShaked)
			{
				node.DisconnectAsync();
				return;
			}

			node.Disconnected += OnNodeDisconnected;

			if (_connectedNodes.TryAdd(peerInfo.Endpoint, (node, peerInfo, DateTimeOffset.UtcNow)))
			{
				Logger.LogDebug($"Connected to peer {peerInfo.Endpoint} (score: {peerInfo.Score:F1}, services: {peerInfo.Services.AsCsv()}). Total connected peers: {_connectedNodes.Count}.");
				_eventBus.Publish(new P2pNodeAdded(peerInfo.Endpoint, node));
			}
			else
			{
				DisconnectNode(node);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"Failed to connect to {peerInfo.Endpoint}: {ex.Message}");
		}
	}

	private bool TryReserveConnectionAttempt(EndPoint endpoint)
	{
		if (_connectedNodes.ContainsKey(endpoint))
		{
			Logger.LogDebug($"Already connected to peer: {endpoint}");
			return false;
		}

		if (_connectionAttempts.TryGetValue(endpoint, out var last) &&
		    DateTimeOffset.UtcNow - last < ReconnectCooldown)
		{
			Logger.LogDebug($"Connection attempt to peer: {endpoint} skipped (too often)");
			return false;
		}

		_connectionAttempts[endpoint] = DateTimeOffset.UtcNow;
		return true;
	}

	private void OnNodeDisconnected(Node node)
	{
		if (_connectedNodes.TryRemove(node.Peer.Endpoint, out var removed))
		{
			node.Disconnected -= OnNodeDisconnected;
			var connectionDuration = DateTimeOffset.UtcNow - removed.ConnectedAt;
			Logger.LogDebug($"Peer {node.Peer.Endpoint} (score: {removed.PeerInfo.Score:F1}) disconnected after {connectionDuration.TotalSeconds:F1}s. Total connected peers: {_connectedNodes.Count}.");

			if (connectionDuration < QuickDisconnectThreshold)
			{
				_eventBus.Publish(new NodeDisconnectedQuickly(removed.PeerInfo.Endpoint, node));
			}

			_eventBus.Publish(new P2pNodeRemoved(removed.PeerInfo.Endpoint, node));
		}
	}

	private async Task RotateToBetterPeersAsync(CancellationToken cancellationToken)
	{
		var rankedConnectedNodes = _connectedNodes.Values
			.Where(n => n.Node.IsConnected)
			.OrderBy(x => x.PeerInfo.Score)
			.ToArray();

		if (rankedConnectedNodes is not [var worstConnectedNode, ..])
		{
			return;
		}

		var availablePeers = await GetAvailablePeersAsync(cancellationToken).ConfigureAwait(false);
		var bestDiscoveredNode = availablePeers
			.OrderByDescending(p => p.Score)
			.ThenByDescending(p => p.SupportsCompactFilters)
			.FirstOrDefault();

		if (bestDiscoveredNode is null)
		{
			Logger.LogTrace("There is no best peer candidate");
			return;
		}

		var bestDiscoveredScore = bestDiscoveredNode.Score;

		if (bestDiscoveredScore > worstConnectedNode.PeerInfo.Score * RotationScoreThreshold)
		{
			Logger.LogInfo($"Replacing peer {worstConnectedNode.PeerInfo.Endpoint} (score: {worstConnectedNode.PeerInfo.Score:F1}) with {bestDiscoveredNode.Endpoint} (score: {bestDiscoveredScore:F1})");

			if (_connectedNodes.TryRemove(worstConnectedNode.PeerInfo.Endpoint, out var nodeToRemove))
			{
				DisconnectNode(nodeToRemove.Node);
				_eventBus.Publish(new P2pNodeRemoved(nodeToRemove.PeerInfo.Endpoint, nodeToRemove.Node));
			}

			await ConnectToPeerAsync(bestDiscoveredNode, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			Logger.LogDebug($"Peer candidate {bestDiscoveredNode.Endpoint} (score: {bestDiscoveredScore:F1}) is not significantly better than our worst peer (score: {worstConnectedNode.PeerInfo.Score:F1}). Skipping rotation.");
		}
	}

	private void PurgeDisconnectedNodes()
	{
		var dead = _connectedNodes.Where(kv => !kv.Value.Node.IsConnected).ToArray();

		foreach (var (key, (node, _, _)) in dead)
		{
			if (_connectedNodes.TryRemove(key, out _))
			{
				node.Disconnected -= OnNodeDisconnected;
				_eventBus.Publish(new P2pNodeRemoved(key, node));
			}
		}
	}

	private void DisconnectNode(Node node)
	{
		node.Disconnected -= OnNodeDisconnected;
		node.DisconnectAsync();
	}

	private void DisconnectAll()
	{
		foreach (var (_, (node, _, _)) in _connectedNodes)
		{
			DisconnectNode(node);
		}
		_connectedNodes.Clear();
	}

	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		_isDisposed = true;
		DisconnectAll();
		_disposables.Dispose();
	}

	private void ReportMisbehavior(EndPoint endpoint, MisbehaviorType misbehavior) =>
		_discoveryCoordinator?.Post(new NodeMisbehaveMessage(endpoint, misbehavior));

	#region Discovery

	public enum MisbehaviorType
	{
		FailedToConnect,
		DisconnectedQuickly,
		ProvidedInvalidData,
		TimedOutDownloadingBlock,
		Unknown
	}

	private abstract record CoordinatorMessage;
	private record HarvestedEndpointsMessage(EndPoint[] Endpoints) : CoordinatorMessage;
	private record PeerDiscoveredMessage(PeerInfo PeerInfo) : CoordinatorMessage;
	private record NodeMisbehaveMessage(EndPoint Endpoint, MisbehaviorType Behavior) : CoordinatorMessage;
	private record GetPeersMessage(IReplyChannel<PeerInfo[]> ReplyChannel) : CoordinatorMessage;

	private abstract record CrawlerMessage;
	private record CrawlMessage(EndPoint EndPoint) : CrawlerMessage;
	private record SlowDownMessage : CrawlerMessage;

	private record CrawlingCoordinationState(
		ImmutableDictionary<EndPoint, PeerInfo> Peers,
		bool SlowedDown,
		int LastCrawlerIndex);

	private record CrawlerState(
		TimeSpan DelayBeforeVisitingNode);

	private MessageHandler<CoordinatorMessage, CrawlingCoordinationState> CreateDiscovery(
		MailboxProcessor<CrawlerMessage>[] crawlers) =>
		(msg, state, token) => HandleCoordinatorMessageAsync(crawlers, msg, state);

	private Task<CrawlingCoordinationState> HandleCoordinatorMessageAsync(
		MailboxProcessor<CrawlerMessage>[] crawlers,
		CoordinatorMessage msg,
		CrawlingCoordinationState state)
	{
		switch (msg)
		{
			case HarvestedEndpointsMessage(Endpoints: var endpoints):
				var n = state.LastCrawlerIndex;
				foreach (var endPoint in endpoints)
				{
					var wi = n % crawlers.Length;
					crawlers[wi].Post(new CrawlMessage(endPoint));
					n++;
				}
				state = state with { LastCrawlerIndex = n };
				break;

			case PeerDiscoveredMessage(PeerInfo: var peer):
				var updatedPeer = state.Peers.TryGetValue(peer.Endpoint, out var existingPeer)
					? existingPeer with { LastSeen = DateTimeOffset.UtcNow, Score = double.Min(70, existingPeer.Score + 2) }
					: peer;

				state = state with { Peers = state.Peers.SetItem(peer.Endpoint, updatedPeer) };

				if (state is { SlowedDown: false, Peers.Count: > 300 })
				{
					var slowDownMessage = new SlowDownMessage();
					foreach (var crawler in crawlers)
					{
						crawler.Post(slowDownMessage);
					}
					state = state with { SlowedDown = true };
				}
				break;

			case NodeMisbehaveMessage(Endpoint: var offendingEndpoint, Behavior: var behavior):
				if (state.Peers.TryGetValue(offendingEndpoint, out var offendingNode))
				{
					state = behavior switch
					{
						MisbehaviorType.TimedOutDownloadingBlock when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, behavior),
						MisbehaviorType.DisconnectedQuickly when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, behavior),
						MisbehaviorType.FailedToConnect when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, behavior),
						_ =>
							Remove(offendingEndpoint)
					};
				}
				break;

			case GetPeersMessage(ReplyChannel: var replyChannel):
				replyChannel.Reply(state.Peers.Values.ToArray());
				break;
		}

		return Task.FromResult(state);

		CrawlingCoordinationState Punish(EndPoint offendingEndpoint, PeerInfo offendingNode, MisbehaviorType misbehaviorType)
		{
			var newPeerInfo = offendingNode with { Score = offendingNode.Score - 10 };
			Logger.LogDebug($"Peer {offendingNode.Endpoint} was punished for {misbehaviorType}. Score {offendingNode.Score:F1} -> {newPeerInfo.Score:F1}.");
			return state with
			{
				Peers = state.Peers.SetItem(offendingEndpoint, offendingNode with {Score = offendingNode.Score - 10})
			};
		}

		CrawlingCoordinationState Remove(EndPoint offendingEndpoint) =>
			state with { Peers = state.Peers.Remove(offendingEndpoint) };
	}

	private MessageHandler<CrawlerMessage, CrawlerState> CreateCrawler() =>
		async (msg, state, token) => await HandleCrawlerMessageAsync(msg, state, token).ConfigureAwait(false);

	private async Task<CrawlerState> HandleCrawlerMessageAsync(
		CrawlerMessage msg,
		CrawlerState state,
		CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case CrawlMessage(var endpoint):
				await Task.Delay(state.DelayBeforeVisitingNode, cancellationToken).ConfigureAwait(false);
				Node? node = null;
				try
				{
					node = await VisitEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
					if (node is not null)
					{
						await HarvestAddressesAsync(node, cancellationToken).ConfigureAwait(false);
					}
				}
				finally
				{
					node?.DisconnectAsync();
				}
				break;
			case SlowDownMessage:
				return new CrawlerState(DelayBeforeVisitingNode: state.DelayBeforeVisitingNode + TimeSpan.FromSeconds(1));
		}

		return state;
	}

	private async Task<Node?> VisitEndpointAsync(EndPoint endpoint, CancellationToken cancellationToken)
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(CrawlerConnectionTimeout);

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
				return null;
			}
			connParams.TemplateBehaviors.Add(new SocksSettingsBehavior(_torSocks5));
		}

		Node? node = null;
		var sw = Stopwatch.StartNew();
		try
		{
			node = await Node.ConnectAsync(_network, endpoint, connParams).ConfigureAwait(false);
			await node.VersionHandshakeAsync(timeoutCts.Token).ConfigureAwait(false);

			sw.Stop();

			if (node.State != NodeState.HandShaked)
			{
				_discoveryCoordinator?.Post(new NodeMisbehaveMessage(endpoint, MisbehaviorType.FailedToConnect));
				node.DisconnectAsync();
				return null;
			}

			var now = DateTimeOffset.UtcNow;
			var pv = node.PeerVersion;
			var peer = new PeerInfo(
				endpoint: endpoint,
				userAgent: pv.UserAgent ?? "Unknown",
				protocolVersion: pv.Version,
				services: pv.Services,
				startHeight: pv.StartHeight,
				connectionTime: sw.Elapsed,
				discoveredAt: now,
				lastSeen: now);

			Logger.LogDebug($"Connected to endpoint '{endpoint}'");
			_discoveryCoordinator?.Post(new PeerDiscoveredMessage(peer));
		}
		catch (Exception e)
		{
			Logger.LogTrace($"Failed to connect to endpoint '{endpoint}' (requested cancellation: {cancellationToken.IsCancellationRequested})", e);

			_discoveryCoordinator?.Post(new NodeMisbehaveMessage(endpoint, MisbehaviorType.FailedToConnect));
			node?.DisconnectAsync();
			return null;
		}
		return node;
	}

	private async Task HarvestAddressesAsync(Node node, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<EndPoint[]>(TaskCreationOptions.RunContinuationsAsynchronously);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(CrawlerHarvestTimeout);
		var token = timeoutCts.Token;
		var _ = token.Register(() => tcs.TrySetCanceled(token));

		node.MessageReceived += OnMessage;
		try
		{
			await node.SendMessageAsync(new GetAddrPayload()).ConfigureAwait(false);
			var harvestedEndpoints = await tcs.Task.ConfigureAwait(false);
			_discoveryCoordinator?.Post(new HarvestedEndpointsMessage(harvestedEndpoints));
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
			foreach (var a in addr.Addresses.OrderByDescending(x => x.Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS)))
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
				return await _dnsResolver.GetHostAddressesAsync(dnsServerHost, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				return e;
			}
		}

		var dnsHosts = _network.DNSSeeds.Select(x => x.Host);
		if (_dnsResolver is DnsSocksResolver)
		{
			dnsHosts = Enumerable.Repeat(dnsHosts, 16).SelectMany(x => x).Shuffle();
		}
		var tasks = dnsHosts.Select(GetAddressesFromDnsAsync);

		await foreach (var task in Task.WhenEach(tasks).WithCancellation(cancellationToken))
		{
			var dnsQueryResult = await task.ConfigureAwait(false);

			if (dnsQueryResult.IsOk)
			{
				var endpoints = dnsQueryResult.Value
					.Select(x => new IPEndPoint(x.MapToIPv6(), _network.DefaultPort))
					.Cast<EndPoint>()
					.ToArray();
				_discoveryCoordinator?.Post(new HarvestedEndpointsMessage(endpoints));
			}
		}

		var endpointsFromSeedNodes = _network.SeedNodes
			.Select(x => x.Endpoint)
			.ToArray();

		_discoveryCoordinator?.Post(new HarvestedEndpointsMessage(endpointsFromSeedNodes));
	}

	#endregion
}

public static class Extensions
{
	public static string AsCsv(this NodeServices ns)
	{
		(NodeServices, string)[] flagNames =
		[
			(NodeServices.Network, "Blocks"),
			(NodeServices.GetUTXO, "UTXO"),
			(NodeServices.NODE_BLOOM, "Bloom Filters"),
			(NodeServices.NODE_COMPACT_FILTERS, "Compact Filters"),
			(NodeServices.NODE_NETWORK_LIMITED, "Limited Network"),
			(NodeServices.NODE_WITNESS, "Witness"),
			((NodeServices)2048, "P2P v2")
		];

		var nsCopy = ns;
		var supportedServices = new List<string>(7);

		foreach (var (flag, name) in flagNames)
		{
			if (ns.HasFlag(flag))
			{
				supportedServices.Add(name);
				nsCopy &= ~flag;
			}
		}

		if (nsCopy > 0)
		{
			supportedServices.Add(((long)nsCopy).ToString());
		}

		return string.Join(" | ", supportedServices);
	}
}
