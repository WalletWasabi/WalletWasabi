using System.Collections.Generic;
using System.Linq;
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
	public async Task FilterHeadersWaitForTheNodesFilterRangeAsync()
	{
		// Block header 1 and its filter header are known, so the only request to make is the filter range 1-1.
		var blockHeaderChain = BuildBlockHeaderChain(1);
		var block1 = blockHeaderChain.GetBlock(1);
		var genesisFilterHeader = FilterCheckpoints.GetWasabiGenesisFilter(Network).Header;
		var filter = new GolombRiceFilterBuilder().SetKey(block1.HashBlock).AddEntries([new byte[] { 1, 2, 3 }]).Build();
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(genesisFilterHeader);
		filterHeaderChain.AppendTip(new SmartHeader(block1.HashBlock, filter.GetHeader(genesisFilterHeader.BlockFilterHeader), 1, 0L));

		var eventBus = new EventBus();
		var synchronizationState = new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
		using var fakeNode = new FakeNode();
		using var node = await Node.ConnectAsync(Network, fakeNode.EndPoint);
		node.Behaviors.Add(new CompactFilterBehavior(synchronizationState, blockHeaderChain, eventBus));
		await node.VersionHandshakeAsync();

		await fakeNode.WaitForRequestsAsync(1);
		Assert.Equal(["getcfilters"], fakeNode.Requests);

		// 2000 new block headers make a filter header range assignable while the filter range is still streaming.
		Extend(blockHeaderChain, 2000);
		eventBus.Publish(new Tick(DateTime.UtcNow));
		await Task.Delay(TimeSpan.FromSeconds(1));
		Assert.Equal(["getcfilters"], fakeNode.Requests);

		// Only once the filter range is complete does the node get the filter header range.
		fakeNode.Send(new CompactFilterPayload(FilterType.Basic, block1.HashBlock, filter.ToBytes()));
		await fakeNode.WaitForRequestsAsync(2);
		Assert.Equal(["getcfilters", "getcfheaders"], fakeNode.Requests);
	}

	[Fact]
	public async Task FiltersWaitForTheNodesFilterHeaderReplyAsync()
	{
		// Block headers up to 1200, filter headers up to 1000: a filter header range (1001-1200) and a filter range
		// (1-500) can be assigned at the same time.
		var blockHeaderChain = BuildBlockHeaderChain(1200);
		var filterHeaderChain = new FilterHeaderChain();
		filterHeaderChain.AppendTip(FilterCheckpoints.GetWasabiGenesisFilter(Network).Header);
		for (var height = 1u; height <= 1000; height++)
		{
			filterHeaderChain.AppendTip(new SmartHeader(blockHeaderChain.GetBlock((int)height).HashBlock, new uint256(height), height, 0L));
		}

		var synchronizationState = new FilterSynchronizationState(blockHeaderChain, filterHeaderChain, tipHeight: 0);
		using var fakeNode = new FakeNode();
		using var node = await Node.ConnectAsync(Network, fakeNode.EndPoint);
		node.Behaviors.Add(new CompactFilterBehavior(synchronizationState, blockHeaderChain, new EventBus()));
		await node.VersionHandshakeAsync();

		await fakeNode.WaitForRequestsAsync(1);
		await Task.Delay(TimeSpan.FromSeconds(1));
		Assert.Equal(["getcfheaders"], fakeNode.Requests);

		// Only once the filter headers arrived does the node get the filter range.
		fakeNode.Send(new CompactFilterHeadersPayload
		{
			StopHash = blockHeaderChain.GetBlock(1200).HashBlock,
			PreviousFilterHeader = filterHeaderChain.Tip!.BlockFilterHeader,
			FilterHeaders = Enumerable.Range(1001, 200).Select(h => new uint256((ulong)h)).ToList(),
		});

		await fakeNode.WaitForRequestsAsync(2);
		Assert.Equal(["getcfheaders", "getcfilters"], fakeNode.Requests);
		Assert.Equal(1200u, (uint)filterHeaderChain.Tip.Height);
	}

	private static ConcurrentChain BuildBlockHeaderChain(int height)
	{
		var chain = new ConcurrentChain(Network);
		Extend(chain, height);
		return chain;
	}

	private static void Extend(ConcurrentChain chain, int count)
	{
		var tip = chain.Tip;
		for (var i = 0; i < count; i++)
		{
			var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.HashPrevBlock = tip.HashBlock;
			header.Nonce = (uint)(tip.Height + 1);
			tip = new ChainedBlock(header, header.GetHash(), tip);
		}

		chain.SetTip(tip);
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
