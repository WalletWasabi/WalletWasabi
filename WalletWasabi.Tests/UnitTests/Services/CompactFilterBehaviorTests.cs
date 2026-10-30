using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using Xunit;
namespace WalletWasabi.Tests.UnitTests.Services;

public class CompactFilterBlockHashReproTests
{
	[Fact]
	public void ValidFilterBytesAreAcceptedWithAnAttackerChosenBlockHash()
	{
		var network = Network.RegTest;
		var blockHeaderChain = new ConcurrentChain(network);
		var header = network.Consensus.ConsensusFactory.CreateBlockHeader();
		header.HashPrevBlock = blockHeaderChain.Tip.HashBlock;
		header.Nonce = 1;
		var canonicalBlock = new ChainedBlock(header, header.GetHash(), blockHeaderChain.Tip);
		blockHeaderChain.SetTip(canonicalBlock);

		var targetScript = Script.FromHex("00141111111111111111111111111111111111111111");
		var unrelatedScript = Script.FromHex("00142222222222222222222222222222222222222222");
		var legitimateFilter = new GolombRiceFilterBuilder()
			.SetKey(canonicalBlock.HashBlock)
			.AddEntries([unrelatedScript.ToBytes()])
			.Build();
		Assert.False(legitimateFilter.MatchAny([targetScript.ToBytes()], canonicalBlock.HashBlock.ToBytes()[..16]));

		// The peer cannot change committed filter bytes, but it can freely choose the
		// unvalidated BlockHash field. This precomputed hash changes the BIP158 key and
		// turns targetScript into a false-positive match against the same filter bytes.
		var markerHash = new uint256(0x1816ful);
		Assert.NotEqual(canonicalBlock.HashBlock, markerHash);
		Assert.True(legitimateFilter.MatchAny([targetScript.ToBytes()], markerHash.ToBytes()[..16]));

		var genesisFilter = FilterCheckpoints.GetWasabiGenesisFilter(network);
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(genesisFilter.Header);
		var expectedFilterHeader = legitimateFilter.GetHeader(genesisFilter.Header.BlockFilterHeader);
		filterHeaderChain.AppendTip(
			new SmartHeader(canonicalBlock.HashBlock, expectedFilterHeader, 1, header.BlockTime));

		var synchronizationState = new FilterSynchronizationState(
			blockHeaderChain,
			filterHeaderChain,
			tipHeight: 0);
		var behavior = new CompactFilterBehavior(synchronizationState, blockHeaderChain, new EventBus());
		// Notice that the compact filter contains markerHash block hash instead of the canonical block hash. That's the attacker's choice.
		var payload = new CompactFilterPayload(FilterType.Basic, markerHash, legitimateFilter.ToBytes());

		var result = behavior.ValidateFilters(1u, [payload], network);

		Assert.Null(result);
	}
}

/// <summary>Drives <see cref="CompactFilterBehavior"/> against an in-process node that advertises compact filters.</summary>
public class CompactFilterBehaviorTests
{
	private static readonly Network Network = Network.RegTest;

	[Fact]
	public async Task FilterRangeTimeoutCountsFromTheLastReceivedFilterAsync()
	{
		// Two blocks with their filter headers known: the node is asked for the filter range 1-2.
		var blockHeaderChain = BuildBlockHeaderChain(2);
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetWasabiGenesisFilter(Network).Header);
		for (var height = 1u; height <= 2; height++)
		{
			filterHeaderChain.AppendTip(new SmartHeader(blockHeaderChain.GetBlock((int)height).HashBlock, new uint256(height), height, 0L));
		}

		var eventBus = new EventBus();
		var synchronizationState = new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
		using var fakeNode = new FakeNode();
		using var node = await Node.ConnectAsync(Network, fakeNode.EndPoint);
		node.Behaviors.Add(new CompactFilterBehavior(synchronizationState, blockHeaderChain, eventBus));
		await node.VersionHandshakeAsync();

		await fakeNode.WaitForRequestsAsync(1);
		var requestedAt = DateTime.UtcNow;
		Assert.Equal(["getcfilters"], fakeNode.Requests);

		// The first filter arrives 6 s after the request. 33 s after the request the range is over the 30 s clearnet
		// timeout, but the node has been silent for 27 s only, so it is kept.
		await Task.Delay(TimeSpan.FromSeconds(6));
		var block1 = blockHeaderChain.GetBlock(1);
		var filter = new GolombRiceFilterBuilder().SetKey(block1.HashBlock).AddEntries([new byte[] { 1, 2, 3 }]).Build();
		fakeNode.Send(new CompactFilterPayload(FilterType.Basic, block1.HashBlock, filter.ToBytes()));
		await Task.Delay(TimeSpan.FromSeconds(1));
		eventBus.Publish(new Tick(requestedAt + TimeSpan.FromSeconds(33)));
		await Task.Delay(TimeSpan.FromSeconds(1));
		Assert.Equal(NodeState.HandShaked, node.State);

		// Nothing follows that filter: once the silence exceeds the timeout the node is dropped and its range is free again.
		eventBus.Publish(new Tick(requestedAt + TimeSpan.FromSeconds(70)));
		await WaitForDisconnectAsync(node);
		Assert.NotEqual(NodeState.HandShaked, node.State);
		Assert.True(synchronizationState.TryAssignFilterRange(out var releasedRange));
		Assert.Equal(1u, releasedRange.StartHeight);
	}

	private static async Task WaitForDisconnectAsync(Node node)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
		while (node.State == NodeState.HandShaked && DateTime.UtcNow < deadline)
		{
			await Task.Delay(50);
		}
	}

	private static ConcurrentChain BuildBlockHeaderChain(int height)
	{
		var chain = new ConcurrentChain(Network);
		var tip = chain.Tip;
		for (var i = 1; i <= height; i++)
		{
			var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.HashPrevBlock = tip.HashBlock;
			header.Nonce = (uint)i;
			tip = new ChainedBlock(header, header.GetHash(), tip);
		}

		chain.SetTip(tip);
		return chain;
	}

	private sealed class FakeNode : IDisposable
	{
		private readonly NodeServer _server;
		private readonly List<string> _requests = [];
		private Node? _client;

		public FakeNode()
		{
			_server = new NodeServer(Network) { LocalEndpoint = new IPEndPoint(IPAddress.Loopback, FreePort()), AllowLocalPeers = true };
			_server.InboundNodeConnectionParameters.TemplateBehaviors.Clear();
			_server.InboundNodeConnectionParameters.Services = NodeServices.NODE_COMPACT_FILTERS;
			_server.MessageReceived += OnMessageReceived;
			_server.Listen();
		}

		public IPEndPoint EndPoint => _server.LocalEndpoint;

		public string[] Requests
		{
			get
			{
				lock (_requests)
				{
					return [.. _requests];
				}
			}
		}

		public void Send(Payload payload) => _client!.SendMessageAsync(payload);

		public async Task WaitForRequestsAsync(int count)
		{
			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
			while (Requests.Length < count && DateTime.UtcNow < deadline)
			{
				await Task.Delay(50);
			}
		}

		private void OnMessageReceived(NodeServer server, IncomingMessage message)
		{
			if (message.Message.Payload is not (GetCompactFilterHeadersPayload or GetCompactFiltersPayload))
			{
				return;
			}

			_client = message.Node;
			lock (_requests)
			{
				_requests.Add(message.Message.Command);
			}
		}

		private static int FreePort()
		{
			using var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			return ((IPEndPoint)listener.LocalEndpoint).Port;
		}

		public void Dispose() => _server.Dispose();
	}
}
