using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Services;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Services;

/// <summary>
/// Drives <see cref="BlockHeaderSyncBehavior"/> against an in-process node that answers <c>getheaders</c> like Bitcoin Core:
/// headers after the first locator hash it knows, or from genesis when it knows none.
/// </summary>
/// <remarks>The node is given a fixed time to answer, which it can miss while other tests are running.</remarks>
/// <seealso cref="XunitConfiguration.SerialCollectionDefinition"/>
[Collection("Serial unit tests collection")]
public class BlockHeaderSyncBehaviorTests
{
	private static readonly Network Network = Network.RegTest;

	[Fact]
	public async Task SyncsFromACheckpointAcrossARetargetAsync()
	{
		var nodeHeaders = MineChain(count: 4116);
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(nodeHeaders[2016], 2016));

		using var fakeNode = new FakeNode(nodeHeaders);
		var requests = await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(4116, chain.Tip!.Height);
		Assert.Equal(nodeHeaders[4116].GetHash(), chain.Tip.HashBlock);
		Assert.Equal(2, requests); // 2000 headers, then the remaining 100.
	}

	[Fact]
	public async Task HeadersThatDoNotConnectAreNotRequestedAgainAsync()
	{
		var nodeHeaders = MineChain(count: 2100);
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(MineChain(count: 1, salt: 1)[1], 2016)); // A base the node has never heard of.

		using var fakeNode = new FakeNode(nodeHeaders);
		var requests = await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(2016, chain.Tip!.Height);
		Assert.Equal(1, requests);
	}

	[Fact]
	public async Task APeerBehindUsIsNotAskedAgainAsync()
	{
		var headers = MineChain(count: 6116);
		var chain = ChainFrom(headers, baseHeight: 2016, tipHeight: 6116);

		using var fakeNode = new FakeNode(headers.Take(4051).ToList()); // The node is at 4050.
		var requests = await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(6116, chain.Tip!.Height);
		Assert.Equal(1, requests); // Its answer, 2017-4016, is all known: there is nothing to continue from.
	}

	[Fact]
	public async Task ReplayedKnownHeadersAreNotRequestedAgainAsync()
	{
		var headers = MineChain(count: 6116);
		var chain = ChainFrom(headers, baseHeight: 2016, tipHeight: 6116);

		using var fakeNode = new FakeNode(headers, alwaysAnswersFrom: 2017); // Ignores the locator and replays 2017-4016.
		var requests = await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(6116, chain.Tip!.Height);
		Assert.Equal(1, requests);
	}

	[Fact]
	public async Task APeerWithNothingNewStillTellsTheNetworkTipAsync()
	{
		var headers = MineChain(count: 2100);
		var chain = ChainFrom(headers, baseHeight: 2016, tipHeight: 2100); // Restarted with no new block: already at the peer's tip.
		var filterHeaderChain = new FilterHeaderChain();

		using var fakeNode = new FakeNode(headers);
		var requests = await fakeNode.SyncAsync(chain, filterHeaderChain, settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(2100, chain.Tip!.Height);
		Assert.Equal(2100u, (uint)filterHeaderChain.ServerTipHeight); // The wallets wait for this before they load.
		Assert.Equal(1, requests);
	}

	[Fact]
	public async Task BlockHeadersAreSynchronizedWhenThePeerHasNoMoreAsync()
	{
		var nodeHeaders = MineChain(count: 4116);
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(nodeHeaders[2016], 2016));
		var synchronizationState = new BlockHeaderSynchronizationState();

		using var fakeNode = new FakeNode(nodeHeaders);
		await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2), synchronizationState);

		Assert.Equal(4116, chain.Tip!.Height);
		Assert.True(synchronizationState.IsSynchronized); // The second message, of 100 headers, was not full.
	}

	[Fact]
	public async Task BlockHeadersAreSynchronizedWhenThePeerHasNothingNewAsync()
	{
		var headers = MineChain(count: 2100);
		var chain = ChainFrom(headers, baseHeight: 2016, tipHeight: 2100); // Restarted with no new block: already at the peer's tip.
		var synchronizationState = new BlockHeaderSynchronizationState();

		using var fakeNode = new FakeNode(headers);
		await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2), synchronizationState);

		Assert.True(synchronizationState.IsSynchronized); // What is downloaded after the block headers waits for this.
	}

	[Fact]
	public async Task BlockHeadersAreNotSynchronizedWhileThePeerHasMoreAsync()
	{
		var headers = MineChain(count: 6116);
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(headers[2016], 2016));
		var synchronizationState = new BlockHeaderSynchronizationState();

		using var fakeNode = new FakeNode(headers, alwaysAnswersFrom: 2017); // Sends 2017-4016, then the same again instead of the rest.
		await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2), synchronizationState);

		Assert.Equal(4016, chain.Tip!.Height);
		Assert.False(synchronizationState.IsSynchronized); // Every message was full: the peer has more headers.
	}

	[Fact]
	public async Task APeerBehindUsDoesNotTellThatBlockHeadersAreSynchronizedAsync()
	{
		var headers = MineChain(count: 6116);
		var chain = ChainFrom(headers, baseHeight: 2016, tipHeight: 6116);
		var synchronizationState = new BlockHeaderSynchronizationState();

		using var fakeNode = new FakeNode(headers.Take(2117).ToList()); // The node is at 2116.
		await fakeNode.SyncAsync(chain, new FilterHeaderChain(), settleTime: TimeSpan.FromSeconds(2), synchronizationState);

		Assert.Equal(6116, chain.Tip!.Height);
		Assert.False(synchronizationState.IsSynchronized); // Its answer, 2017-2116, ends below the tip of the chain.
	}

	/// <summary>Mines a regtest header chain; index = height, so <c>[0]</c> is genesis.</summary>
	private static List<BlockHeader> MineChain(int count, uint salt = 0)
	{
		var headers = new List<BlockHeader> { Network.GetGenesis().Header };
		var tip = new ChainedBlock(headers[0], 0);
		for (var i = 1; i <= count; i++)
		{
			var header = Network.Consensus.ConsensusFactory.CreateBlockHeader();
			header.Version = 0x20000000;
			header.HashPrevBlock = tip.HashBlock;
			header.HashMerkleRoot = new uint256((ulong)i + salt);
			header.BlockTime = tip.Header.BlockTime + TimeSpan.FromMinutes(10);
			header.Bits = tip.GetNextWorkRequired(Network);
			while (!header.CheckProofOfWork())
			{
				header.Nonce++;
			}

			tip = new ChainedBlock(header, header.GetHash(), tip);
			headers.Add(header);
		}

		return headers;
	}

	private static ConcurrentChain ChainFrom(List<BlockHeader> headers, int baseHeight, int tipHeight)
	{
		var tip = new ChainedBlock(headers[baseHeight], baseHeight);
		for (var height = baseHeight + 1; height <= tipHeight; height++)
		{
			tip = new ChainedBlock(headers[height], headers[height].GetHash(), tip);
		}

		var chain = new ConcurrentChain();
		chain.SetTip(tip);
		return chain;
	}

	private sealed class FakeNode : IDisposable
	{
		private readonly List<BlockHeader> _headers;
		private readonly Dictionary<uint256, int> _heights;
		private readonly NodeServer _server;
		private readonly int? _alwaysAnswersFrom;
		private int _getHeadersReceived;

		public FakeNode(List<BlockHeader> headers, int? alwaysAnswersFrom = null)
		{
			_headers = headers;
			_alwaysAnswersFrom = alwaysAnswersFrom;
			_heights = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h.GetHash(), x => x.i);
			_server = new NodeServer(Network) { LocalEndpoint = new IPEndPoint(IPAddress.Loopback, FreePort()), AllowLocalPeers = true };
			_server.InboundNodeConnectionParameters.TemplateBehaviors.Clear();
			_server.MessageReceived += OnMessageReceived;
			_server.Listen();
		}

		public async Task<int> SyncAsync(ConcurrentChain chain, FilterHeaderChain filterHeaderChain, TimeSpan settleTime, BlockHeaderSynchronizationState? synchronizationState = null)
		{
			using var node = await Node.ConnectAsync(Network, _server.LocalEndpoint);
			node.Behaviors.Add(new BlockHeaderSyncBehavior(chain, synchronizationState ?? new BlockHeaderSynchronizationState(), filterHeaderChain, new EventBus()));
			await node.VersionHandshakeAsync();
			await Task.Delay(settleTime);
			node.Disconnect();
			return _getHeadersReceived;
		}

		private void OnMessageReceived(NodeServer server, IncomingMessage message)
		{
			if (message.Message.Payload is not GetHeadersPayload getHeaders)
			{
				return;
			}

			Interlocked.Increment(ref _getHeadersReceived);
			var start = _alwaysAnswersFrom ?? getHeaders.BlockLocators.Blocks.Select(h => _heights.TryGetValue(h, out var height) ? height : -1).FirstOrDefault(h => h >= 0, 0) + 1;
			var batch = _headers.Skip(start).Take(2000).ToArray();
			message.Node.SendMessageAsync(new HeadersPayload(batch));
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
