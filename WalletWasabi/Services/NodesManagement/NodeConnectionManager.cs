using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Services.NodesManagement;

public class NodesRegistry : IDisposable
{
	private readonly ConcurrentDictionary<EndPoint, Node> _nodes = [];
	private readonly ComposedDisposable _disposables = new();

	public NodesRegistry(EventBus eventBus)
	{
		eventBus.Subscribe<BitcoinNodeAdded>(e => _nodes.TryAdd(e.EndPoint, e.Node)).DisposeUsing(_disposables);
		eventBus.Subscribe<BitcoinNodeRemoved>(e => _nodes.TryRemove(e.EndPoint, out _)).DisposeUsing(_disposables);
	}

	public int Count => _nodes.Count;
	public Node[] Nodes => _nodes.Values.ToArray();

	public void Dispose()
	{
		_disposables.Dispose();
	}
}

public class NodeConnectionManager : IDisposable
{
	private const int TargetConnections = 12;
	private const int MinCompactFilterNodes = 5;
	private const double RotationScoreThreshold = 1.20;

	private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan QuickDisconnectThreshold = TimeSpan.FromSeconds(30);
	private const int MaintainEverySeconds = 5;
	private const int RotateEverySeconds = 300;

	private readonly Network _network;
	private readonly NodeDiscoveryCoordinator.PeersInfoProvider _getPeersInfo;
	private readonly Func<NodeBehavior>[] _behaviorFactories;
	private readonly EventBus _eventBus;
	private readonly TimeSpan _connectionTimeout;
	private readonly EndPoint? _torSocks5Endpoint;
	private readonly IDisposable _heightSubscription;

	private readonly ConcurrentDictionary<EndPoint, (Node Node, PeerInfo PeerInfo, DateTimeOffset ConnectedAt)> _connectedNodes = new();
	private readonly ConcurrentDictionary<EndPoint, DateTimeOffset> _connectionAttempts = new();

	private int _currentTipHeight;
	private int _tickCounter;
	private bool _isDisposed;

	public NodeConnectionManager(
		Network network,
		NodeDiscoveryCoordinator.PeersInfoProvider getPeersInfo,
		Func<NodeBehavior>[] behaviorFactories,
		EventBus eventBus,
		TimeSpan? connectionTimeout = null,
		EndPoint? torSocks5 = null)
	{
		_network = network;
		_getPeersInfo = getPeersInfo;
		_behaviorFactories = behaviorFactories;
		_eventBus = eventBus;
		_connectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(15);
		_torSocks5Endpoint = torSocks5;

		_heightSubscription = eventBus.Subscribe<NetworkTipHeightChanged>(e => _currentTipHeight = (int)e.Height);
	}

	public Node[] Nodes => _connectedNodes.Values
		.Select(x => x.Node)
		.Where(n => n.IsConnected)
		.ToArray();

	public int Count => Nodes.Length;

	public async Task ReevaluateConnectionsAsync(CancellationToken cancellationToken)
	{
		_tickCounter++;

		if (_tickCounter % MaintainEverySeconds == 0 || Count == 0)
		{
			await ConnectToBestPeersAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_tickCounter % RotateEverySeconds == 0 && Count > (TargetConnections - 3))
		{
			await RotateToBetterPeersAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task ConnectToBestPeersAsync(CancellationToken cancellationToken)
	{
		PurgeDisconnectedNodes();

		var currentCount = Count;
		if (currentCount >= TargetConnections)
		{
			return;
		}

		var filterNodeCount = _connectedNodes.Values
			.Count(n => n.Node.IsConnected && n.PeerInfo.SupportsCompactFilters);

		var peers = await SelectPeersForConnectionAsync(
			filterNodesNeeded: Math.Max(0, MinCompactFilterNodes - filterNodeCount),
			totalNeeded: TargetConnections - currentCount,
			cancellationToken).ConfigureAwait(false);

		if (peers.Length == 0)
		{
			return;
		}

		Logger.LogDebug($"Connecting to {peers.Length} peers (current: {currentCount}).");
		await Task.WhenAll(peers.Select(p => ConnectToPeerAsync(p, cancellationToken))).ConfigureAwait(false);
	}

	private async Task<PeerInfo[]> SelectPeersForConnectionAsync(int filterNodesNeeded, int totalNeeded, CancellationToken cancellationToken)
	{
		if (totalNeeded <= 0)
		{
			return [];
		}

		var connectedKeys = _connectedNodes.Keys.ToHashSet();
		var now = DateTimeOffset.UtcNow;
		var cooldownEndpoints = _connectionAttempts
			.Where(kvp => now - kvp.Value < ReconnectCooldown)
			.Select(kvp => kvp.Key)
			.ToHashSet();

		bool IsAvailable(EndPoint endpoint) =>
			!connectedKeys.Contains(endpoint) && !cooldownEndpoints.Contains(endpoint);

		var peers = await _getPeersInfo(cancellationToken).ConfigureAwait(false);
		var filterPeers = filterNodesNeeded > 0
			? peers
				.Where(p => p.SupportsCompactFilters)
				.OrderByDescending(p => p.ComputeScore())
				.Take(filterNodesNeeded * 2)
				.ToArray()
			: [];

		var otherPeers = peers.Except(filterPeers)
			.OrderByDescending(p => p.ComputeScore())
			.Take(totalNeeded * 2)
			.ToArray();

		return filterPeers
			.Concat(otherPeers)
			.Where(p => IsAvailable(p.Endpoint))
			.DistinctBy(p => p.Endpoint)
			.Take(totalNeeded)
			.ToArray();
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
				IsRelay = false,
				UserAgent = Constants.UserAgents[Random.Shared.Next(Constants.UserAgents.Length)]
			};

			// Add Tor support if endpoint is onion
			if (peerInfo.Endpoint.IsTor() && _torSocks5Endpoint is { } torEndpoint)
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

			// Attach behaviors (each node gets its own instances)
			foreach (var behaviorFactory in _behaviorFactories)
			{
				node.Behaviors.Add(behaviorFactory());
			}

			// Subscribe to disconnection
			node.Disconnected += OnNodeDisconnected;

			if (_connectedNodes.TryAdd(peerInfo.Endpoint, (node, peerInfo, DateTimeOffset.UtcNow)))
			{
				Logger.LogDebug($"Connected to peer: {peerInfo.Endpoint} (services: {peerInfo.Services.AsCsv()})");
				_eventBus.Publish(new BitcoinNodeAdded(peerInfo.Endpoint, node));
			}
			else
			{
				// Another connection was added concurrently
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
			Logger.LogDebug($"Peer disconnected: {node.Peer.Endpoint} (after {connectionDuration.TotalSeconds:F1}s)");

			if (connectionDuration < QuickDisconnectThreshold)
			{
				_eventBus.Publish(new NodeDisconnectedQuickly(removed.PeerInfo.Endpoint, node));
			}

			_eventBus.Publish(new BitcoinNodeRemoved(removed.PeerInfo.Endpoint, node));
		}
	}

	private async Task RotateToBetterPeersAsync(CancellationToken cancellationToken)
	{
		// Get current worst-scoring connected peer
		var connectedPeersWithScores = _connectedNodes.Values
			.Where(n => n.Node.IsConnected)
			.Select(n => new
			{
				Node = n,
				Key = n.PeerInfo.Endpoint,
				Score = n.PeerInfo.ComputeScore()
			})
			.OrderBy(x => x.Score)
			.ToArray();

		if (connectedPeersWithScores.Length == 0)
		{
			return;
		}

		var worstConnected = connectedPeersWithScores.First();

		// Get best discovered peer not currently connected and not in cooldown
		var connectedKeys = _connectedNodes.Keys.ToHashSet();
		var now = DateTimeOffset.UtcNow;
		var cooldownEndpoints = _connectionAttempts
			.Where(kvp => now - kvp.Value < ReconnectCooldown)
			.Select(kvp => kvp.Key)
			.ToHashSet();

		bool IsAvailable(EndPoint endpoint) =>
			!connectedKeys.Contains(endpoint) && !cooldownEndpoints.Contains(endpoint);

		var peers = await _getPeersInfo(cancellationToken).ConfigureAwait(false);
		var bestDiscovered = peers
			.OrderByDescending(p => p.ComputeScore())
			.FirstOrDefault(p => IsAvailable(p.Endpoint));

		if (bestDiscovered is null)
		{
			Logger.LogDebug("There no best peer candidate");
			return;
		}

		var bestDiscoveredScore = bestDiscovered.ComputeScore();

		// Only rotate if significantly better (20% threshold)
		if (bestDiscoveredScore > worstConnected.Score * RotationScoreThreshold)
		{
			Logger.LogInfo($"Rotating peer: replacing {worstConnected.Key} (score: {worstConnected.Score:F1}) with {bestDiscovered.Endpoint} (score: {bestDiscoveredScore:F1})");

			// Disconnect worst
			if (_connectedNodes.TryRemove(worstConnected.Key, out var nodeToRemove))
			{
				DisconnectNode(nodeToRemove.Node);
				_eventBus.Publish(new BitcoinNodeRemoved(nodeToRemove.PeerInfo.Endpoint, nodeToRemove.Node));
			}

			// Connect to better peer
			await ConnectToPeerAsync(bestDiscovered, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			Logger.LogDebug($"Best peer candidate for rotation is worst than all already connect peers. Scored {bestDiscoveredScore}");
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
				_eventBus.Publish(new BitcoinNodeRemoved(key, node));
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

		_heightSubscription.Dispose();
		_isDisposed = true;
		DisconnectAll();
	}

}

public static partial class Extensions
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

		var nscopy = ns;
		var supportedServices = new List<string>(7);

		foreach (var (flag, name) in flagNames)
		{
			if (ns.HasFlag(flag))
			{
				supportedServices.Add(name);
				nscopy &= ~flag;
			}
		}

		if (nscopy > 0)
		{
			supportedServices.Add(((long)nscopy).ToString());
		}

		return string.Join(" | ", supportedServices);
	}
}
