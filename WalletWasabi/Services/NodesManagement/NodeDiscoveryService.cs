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

public delegate Task<PeerInfo[]> PeersInfoProvider(CancellationToken cancellationToken);

public static class NodeDiscoveryCoordinator
{
	public static readonly string ServiceName = "BitcoinP2pNodeDiscoveryServiceCoordinator";

	public enum MisbehaviorType
	{
		FailedToConnect,
		DisconnectedQuickly,
		ProvidedInvalidData,
		TimedOutDownloadingBlock
	}

	public abstract record CoordinatorMessage;
	record HarvestedEndpointsMessage(EndPoint[] Endpoints) : CoordinatorMessage;
	record PeerDiscoveredMessage(PeerInfo PeerInfo) : CoordinatorMessage;
	public record NodeMisbehaveMessage(EndPoint Endpoint, MisbehaviorType BehaviorType) : CoordinatorMessage;
	record GetPeersMessage(IReplyChannel<PeerInfo[]> ReplyChannel) : CoordinatorMessage;

	public abstract record CrawlerMessage;
	record CrawlMessage(EndPoint EndPoint) : CrawlerMessage;
	record SlowDownMessage : CrawlerMessage;

	public record CrawlingCoordinationState(
		ImmutableDictionary<EndPoint, PeerInfo> Peers,
		bool SlowedDown,
		int LastCrawlerIndex);

	public record CrawlerState(
		TimeSpan DelayBeforeVisitingNode);

	public static MessageHandler<CoordinatorMessage, CrawlingCoordinationState> CreateDiscovery(
		MailboxProcessor<CrawlerMessage>[] crawlers) =>
		(msg, state, token) => CrawlingAsync(crawlers, msg, state);

	private static Task<CrawlingCoordinationState> CrawlingAsync(MailboxProcessor<CrawlerMessage>[] crawlers, CoordinatorMessage msg, CrawlingCoordinationState state)
	{
		switch (msg)
		{
			case HarvestedEndpointsMessage (Endpoints: var endpoints):
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
					? existingPeer with { LastSeen = DateTimeOffset.UtcNow, Score = double.Min(70, existingPeer.Score + 2) }
					: peer;

				state = state with {Peers = state.Peers.SetItem(peer.Endpoint, updatedPeer)};

				if (state is {SlowedDown: false , Peers.Count: > 300})
				{
					var slowDownMessage = new SlowDownMessage();
					foreach (var crawler in crawlers)
					{
						crawler.Post(slowDownMessage);
					}

					state = state with {SlowedDown = true};
				}
				break;

			case NodeMisbehaveMessage (Endpoint: var offendingEndpoint, BehaviorType: var misbehaviorType):
				if (state.Peers.TryGetValue(offendingEndpoint, out var offendingNode))
				{
					state = misbehaviorType switch
					{
						MisbehaviorType.TimedOutDownloadingBlock when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, misbehaviorType),
						MisbehaviorType.DisconnectedQuickly when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, misbehaviorType),
						MisbehaviorType.FailedToConnect when offendingNode.Score > 30 =>
							Punish(offendingEndpoint, offendingNode, misbehaviorType),
						_ =>
							Remove(offendingEndpoint)
					};
				}
				break;

			case GetPeersMessage (ReplyChannel: var replyChannel):
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
				Peers = state.Peers.SetItem(offendingEndpoint, newPeerInfo)
			};
		}

		CrawlingCoordinationState Remove(EndPoint offendingEndpoint) =>
			state with {Peers = state.Peers.Remove(offendingEndpoint)} ;
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
					if (node is not null)
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
				return new CrawlerState(DelayBeforeVisitingNode: state.DelayBeforeVisitingNode + TimeSpan.FromSeconds(1));
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
			node.VersionHandshake(timeoutCts.Token);

			sw.Stop();

			if (node.State != NodeState.HandShaked)
			{
				NotifyCoordinator(new NodeMisbehaveMessage(endpoint, MisbehaviorType.FailedToConnect));
				node.DisconnectAsync();
				return null;
			}

			var now = DateTimeOffset.UtcNow;
			var pv = node.PeerVersion;
			var peer = new PeerInfo(endpoint: endpoint, userAgent: pv.UserAgent ?? "Unknown",
				protocolVersion: pv.Version, services: pv.Services, startHeight: pv.StartHeight,
				connectionTime: sw.Elapsed, discoveredAt: now, lastSeen: now);

			NotifyCoordinator(new PeerDiscoveredMessage(peer));
		}
		catch
		{
			NotifyCoordinator(new NodeMisbehaveMessage(endpoint, MisbehaviorType.FailedToConnect));
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
				var endpoints = dnsQueryResult.Value
					.Select(x => new IPEndPoint(x.MapToIPv6(), network.DefaultPort))
					.Cast<EndPoint>()
					.ToArray();
				NotifyCoordinator(new HarvestedEndpointsMessage(endpoints));
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

	private static void NotifyCoordinator(CoordinatorMessage msg) =>
		Tell(ServiceName, msg);
}
