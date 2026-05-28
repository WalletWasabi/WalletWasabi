using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Services.NodesManagement;

public static class NodeDiscoveryCoordinator
{
	public static readonly string ServiceName = "BitcoinP2pNodeDiscoveryServiceCoordinator";
	public delegate Task<PeerInfo[]> PeersInfoProvider(CancellationToken cancellationToken);

	public abstract record CoordinatorMessage;
	record HarvestedEndpointsMessage(EndPoint[] Endpoints) : CoordinatorMessage;
	record PeerDiscoveredMessage(PeerInfo PeerInfo) : CoordinatorMessage;
	record PeerFailedMessage(EndPoint Endpoint) : CoordinatorMessage;
	record PeerQuickDisconnectMessage(EndPoint Endpoint) : CoordinatorMessage;
	record GetPeersMessage(IReplyChannel<PeerInfo[]> ReplyChannel) : CoordinatorMessage;

	public abstract record CrawlerMessage;
	record CrawlMessage(EndPoint EndPoint) : CrawlerMessage;
	record SlowDownMessage : CrawlerMessage;
	record StopHarvestingMessage : CrawlerMessage;

	public record CrawlingCoordinationState(
		ImmutableDictionary<EndPoint, PeerInfo> Peers,
		bool Harvesting,
		bool SlowedDown,
		int LastCrawlerIndex);

	public record CrawlerState(
		TimeSpan DelayBeforeVisitingNode,
		bool HarvestEndpoints);

	private static int PriorityOf(CrawlerMessage m) => m switch
	{
		SlowDownMessage => 0,  // highest priority (smallest)
		StopHarvestingMessage  => 1,
		_               => 2,  // lowest priority
	};

	public static readonly Comparer<CrawlerMessage> CrawlerMessagePriority =
		Comparer<CrawlerMessage>.Create((a, b) => PriorityOf(a).CompareTo(PriorityOf(b)));

	public static MessageHandler<CoordinatorMessage, CrawlingCoordinationState> CreateDiscovery(
		MailboxProcessor<CrawlerMessage>[] crawlers) =>
		(msg, state, token) => CrawlingAsync(crawlers, msg, state, token);

	private static Task<CrawlingCoordinationState> CrawlingAsync(MailboxProcessor<CrawlerMessage>[] crawlers, CoordinatorMessage msg, CrawlingCoordinationState state, CancellationToken token)
	{
		switch (msg)
		{
			case HarvestedEndpointsMessage (Endpoints: var endpoints):
				if (state is {Harvesting: true, LastCrawlerIndex: > 15_000})
				{
					var speedUpMessage = new StopHarvestingMessage();
					foreach (var crawler in crawlers)
					{
						crawler.Post(speedUpMessage);
					}

					state = state with {Harvesting = false};
				}
				if (state is {SlowedDown: false , Peers.Count: > 300})
				{
					var speedUpMessage = new SlowDownMessage();
					foreach (var crawler in crawlers)
					{
						crawler.Post(speedUpMessage);
					}

					state = state with {SlowedDown = true};
				}

				var n = state.LastCrawlerIndex;
				foreach (var endPoint in endpoints)
				{
					var wi = n % crawlers.Length;
					crawlers[wi].Post(new CrawlMessage(endPoint));
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
		}

		return Task.FromResult(state);
	}


	public static MessageHandler<CrawlerMessage, CrawlerState> CreateCrawler(Network network,
		EndPoint? torSocks5, TimeSpan connectionTimeout,
		TimeSpan harvestTimeout) =>
		async (msg, state, token) => await ProbeAsync(network, torSocks5, connectionTimeout, harvestTimeout, msg, state, token).ConfigureAwait(false);

	private static async Task<CrawlerState> ProbeAsync(Network network, EndPoint? torSocks5, TimeSpan connectionTimeout,
		TimeSpan harvestTimeout, CrawlerMessage msg, CrawlerState state, CancellationToken cancellationToken)
	{
		switch (msg)
		{
			case CrawlMessage (var endpoint):
				await Task.Delay(state.DelayBeforeVisitingNode, cancellationToken).ConfigureAwait(false);
				Node? node = null;
				try
				{
					node = await VisitEndpointAsync(endpoint, network, torSocks5, connectionTimeout, cancellationToken).ConfigureAwait(false);
					if (node is not null && state.HarvestEndpoints)
					{
						await HarvestAddressesAsync(node, harvestTimeout, cancellationToken).ConfigureAwait(false);
					}
				}
				finally
				{
					node?.DisconnectAsync();
				}
				break;
			case SlowDownMessage:
				return state with {DelayBeforeVisitingNode = state.DelayBeforeVisitingNode + TimeSpan.FromSeconds(1)};
			case StopHarvestingMessage:
				return state with {HarvestEndpoints = false};
		}

		return state;
	}

	private static async Task<Node?> VisitEndpointAsync(EndPoint endpoint, Network network, EndPoint? torSocks5, TimeSpan connectionTimeout, CancellationToken cancellationToken)
	{
		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(connectionTimeout);

		var connParams = new NodeConnectionParameters
		{
			ConnectCancellation = timeoutCts.Token,
			IsRelay = false,
			UserAgent = Constants.UserAgents[Random.Shared.Next(Constants.UserAgents.Length)]
		};

		if (endpoint.IsTor())
		{
			if (torSocks5 is null)
			{
				return null;
			}

			connParams.TemplateBehaviors.Add(new SocksSettingsBehavior(torSocks5));
		}

		Node? node = null;
		var sw = Stopwatch.StartNew();
		try
		{
			node = await Node.ConnectAsync(network, endpoint, connParams).ConfigureAwait(false);
			await node.VersionHandshakeAsync(timeoutCts.Token).ConfigureAwait(false);

			sw.Stop();

			if (node.State != NodeState.HandShaked)
			{
				NotifyCoordinator(new PeerFailedMessage(endpoint));
				node.DisconnectAsync();
				return null;
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
		}
		catch
		{
			NotifyCoordinator(new PeerFailedMessage(endpoint));
			node?.DisconnectAsync();
			return null;
		}
		return node;
	}

	private static async Task HarvestAddressesAsync(Node node, TimeSpan harvestTimeout, CancellationToken cancellationToken)
	{
		var tcs = new TaskCompletionSource<EndPoint[]>(TaskCreationOptions.RunContinuationsAsynchronously);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(harvestTimeout);
		var token = timeoutCts.Token;
		var _ = token.Register(() => tcs.TrySetCanceled(token));

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

	public static async Task SeedFromDnsAsync(Network network, IDnsResolver dns, CancellationToken cancellationToken)
	{
		Logger.LogInfo("Seeding from DNS...");

		async Task<Result<IPAddress[], Exception>> GetAddressesFromDnsAsync(string dnsServerHost)
		{
			try
			{
				return await dns.GetHostAddressesAsync(dnsServerHost, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				return e;
			}
		}

		var dnsHosts = network.DNSSeeds.Select(x => x.Host);
		if (dns is DnsSocksResolver)
		{
			dnsHosts = Enumerable.Repeat(dnsHosts, 16).SelectMany(x => x).Shuffle();
		}
		var tasks = dnsHosts.Select(GetAddressesFromDnsAsync);

		await foreach (var task in Task.WhenEach(tasks).WithCancellation(cancellationToken))
		{
			var dnsQueryResult = await task.ConfigureAwait(false);

			if (dnsQueryResult.IsOk)
			{
				var endpoinst = dnsQueryResult.Value
					.Select(x => new IPEndPoint(x, network.DefaultPort))
					.Cast<EndPoint>()
					.ToArray();
				NotifyCoordinator(new HarvestedEndpointsMessage(endpoinst));
			}
		}

		var endpointsFromSeedNodes = network.SeedNodes
			.Select(x => x.Endpoint)
			.ToArray();

		NotifyCoordinator(new HarvestedEndpointsMessage(endpointsFromSeedNodes));
	}

	public static PeersInfoProvider GetPeersProvider(MailboxProcessor<CoordinatorMessage> coordinator) =>
		async cancellationToken =>
			await coordinator.PostAndReplyAsync<PeerInfo[]>(
			reply => new GetPeersMessage(reply),
			cancellationToken).ConfigureAwait(false);

	public static void ReportQuickDisconnect(MailboxProcessor<CoordinatorMessage> coordinator, EndPoint endpoint) =>
		coordinator.Post(new PeerQuickDisconnectMessage(endpoint));

	private static void NotifyCoordinator(CoordinatorMessage msg) =>
		Tell(ServiceName, msg);
}

public static partial class Extensions
{
	extension(Node node)
	{
		private VersionPayload _PeerVersion
		{
			set => NodeAccessors.GetPeerVersion(node) = value;
		}
		private NodeState _State
		{
			set => NodeAccessors.GetState(node) = value;
		}

		private void _SetVersion(uint version) =>
			NodeAccessors.CallSetVersion(node, version);

		public async Task VersionHandshakeAsync(CancellationToken cancellationToken)
		{
			if (node.State == NodeState.HandShaked)
			{
				throw new InvalidOperationException("Already handshaked");
			}

			using var listener = node.CreateListener()
				.Where(p => p.Message.Payload is VersionPayload or VerAckPayload);

			await node.SendMessageAsync(node.MyVersion).ConfigureAwait(false);

			var version = listener.ReceivePayload<VersionPayload>(cancellationToken);

			node._PeerVersion = version;
			node._SetVersion(Math.Min(node.MyVersion.Version, version.Version));

			if (node.ProtocolCapabilities.PeerTooOld)
			{
				Logger.LogWarning("Outdated version {version} disconnecting", version.Version);
				node.Disconnect("Outdated version");
				return;
			}

			// As a courtesy we do not send sendaddr to nodes that do not support it.
			if (node.ProtocolCapabilities.SupportAddrv2)
			{
				// Signal ADDRv2 support (BIP155).
				await node.SendMessageAsync(new SendAddrV2Payload()).ConfigureAwait(false);
			}

			await node.SendMessageAsync(new VerAckPayload()).ConfigureAwait(false);

			listener.ReceivePayload<VerAckPayload>(cancellationToken);

			node._State = NodeState.HandShaked;

			if (node.Advertize)
			{
				if (node.MyVersion.AddressFrom is IPEndPoint iPEndPoint && !iPEndPoint.Address.IsRoutable(true))
				{
					return;
				}

				await node.SendMessageAsync(new AddrPayload(new NetworkAddress(node.MyVersion.AddressFrom)
				{
					Time = DateTimeOffset.UtcNow
				})).ConfigureAwait(false);
			}
		}
	}

}
public static class NodeAccessors
{
	[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_PeerVersion")]
	public static extern ref VersionPayload GetPeerVersion(Node node);

	[UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_State")]
	public static extern ref NodeState GetState(Node node);

	[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetVersion")]
	public static extern void CallSetVersion(Node node, uint version);
}
