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

public class NodeConnectionManager(
	Network network,
	NodeDiscoveryCoordinator.PeersInfoProvider getPeersInfo,
	Func<NodeBehavior>[] behaviorFactories,
	EventBus eventBus,
	TimeSpan connectionTimeout,
	EndPoint? torSocks5 = null)
	: IDisposable
{
	private const int TargetConnections = 12;
	private const int MinCompactFilterNodes = 5;
	private const double RotationScoreThreshold = 1.1;

	private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan QuickDisconnectThreshold = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan MaintainInterval = TimeSpan.FromSeconds(6);
	private static readonly TimeSpan RotateInterval = TimeSpan.FromMinutes(3);

	private readonly ConcurrentDictionary<EndPoint, (Node Node, PeerInfo PeerInfo, DateTimeOffset ConnectedAt)> _connectedNodes = new();
	private readonly ConcurrentDictionary<EndPoint, DateTimeOffset> _connectionAttempts = new();

	private int _isReevaluating;
	private DateTimeOffset _lastMaintainTime;
	private DateTimeOffset _lastRotateTime;
	private bool _isDisposed;

	public Node[] Nodes => _connectedNodes.Values
		.Select(x => x.Node)
		.Where(n => n.IsConnected)
		.ToArray();

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
			    (now - _lastRotateTime >= TimeSpan.FromSeconds(4) && _connectedNodes.Count(x => x.Value.PeerInfo.SupportsBlocksLimited) < MinCompactFilterNodes))
			{
				_lastRotateTime = now;
				await RotateToBetterPeersAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		finally
		{
			Interlocked.Exchange(ref _isReevaluating, 0);
		}
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
			.ToArray();

		if (peers.Length > 0)
		{
			Logger.LogDebug($"Connecting to {peers.Length} peers (current: {_connectedNodes.Count}).");
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

		var peers = await getPeersInfo(cancellationToken).ConfigureAwait(false);
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
			timeoutCts.CancelAfter(connectionTimeout);

			var connParams = new NodeConnectionParameters
			{
				ConnectCancellation = timeoutCts.Token,
				IsRelay = false,
				UserAgent = Constants.UserAgents[Random.Shared.Next(Constants.UserAgents.Length)]
			};

			// Add Tor support if endpoint is onion
			if (peerInfo.Endpoint.IsTor() && torSocks5 is { } torEndpoint)
			{
				connParams.TemplateBehaviors.Add(new SocksSettingsBehavior(torEndpoint, onlyForOnionHosts: false,
					networkCredential: null, streamIsolation: true));
			}

			var node = await Node.ConnectAsync(network, peerInfo.Endpoint, connParams)
				.ConfigureAwait(false);
			node.VersionHandshake(timeoutCts.Token);

			if (node.State != NodeState.HandShaked)
			{
				node.DisconnectAsync();
				return;
			}

			// Attach behaviors (each node gets its own instances)
			foreach (var behaviorFactory in behaviorFactories)
			{
				node.Behaviors.Add(behaviorFactory());
			}

			// Subscribe to disconnection
			node.Disconnected += OnNodeDisconnected;

			if (_connectedNodes.TryAdd(peerInfo.Endpoint, (node, peerInfo, DateTimeOffset.UtcNow)))
			{
				Logger.LogDebug($"Connected to peer: {peerInfo.Endpoint} (services: {peerInfo.Services.AsCsv()})");
				eventBus.Publish(new BitcoinNodeAdded(peerInfo.Endpoint, node));
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
				eventBus.Publish(new NodeDisconnectedQuickly(removed.PeerInfo.Endpoint, node));
			}

			eventBus.Publish(new BitcoinNodeRemoved(removed.PeerInfo.Endpoint, node));
		}
	}

	private async Task RotateToBetterPeersAsync(CancellationToken cancellationToken)
	{
		// Get current worst-scoring connected peer
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
			.FirstOrDefault();

		if (bestDiscoveredNode is null)
		{
			Logger.LogDebug("There no best peer candidate");
			return;
		}

		var bestDiscoveredScore = bestDiscoveredNode.Score;

		// Only rotate if significantly better
		if (bestDiscoveredScore > worstConnectedNode.PeerInfo.Score * RotationScoreThreshold)
		{
			Logger.LogInfo($"Rotating peer: replacing {worstConnectedNode.PeerInfo.Endpoint} (score: {worstConnectedNode.PeerInfo.Score:F1}) with {bestDiscoveredNode.Endpoint} (score: {bestDiscoveredScore:F1})");

			// Disconnect worst
			if (_connectedNodes.TryRemove(worstConnectedNode.PeerInfo.Endpoint, out var nodeToRemove))
			{
				DisconnectNode(nodeToRemove.Node);
				eventBus.Publish(new BitcoinNodeRemoved(nodeToRemove.PeerInfo.Endpoint, nodeToRemove.Node));
			}

			// Connect to better peer
			await ConnectToPeerAsync(bestDiscoveredNode, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			Logger.LogDebug($"Best peer candidate for rotation is not much better than all already connect peers. Scored {bestDiscoveredScore}");
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
				eventBus.Publish(new BitcoinNodeRemoved(key, node));
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
	}

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
