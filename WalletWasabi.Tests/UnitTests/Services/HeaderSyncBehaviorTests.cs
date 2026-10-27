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
/// Drives <see cref="HeaderSyncBehavior"/> against an in-process node that answers <c>getheaders</c> like Bitcoin Core:
/// headers after the first locator hash it knows, or from genesis when it knows none.
/// </summary>
public class HeaderSyncBehaviorTests
{
	private static readonly Network Network = Network.RegTest;

	[Fact]
	public async Task SyncsFromACheckpointAcrossARetargetAsync()
	{
		var nodeHeaders = MineChain(count: 4116);
		var chain = new ConcurrentChain();
		chain.SetTip(new ChainedBlock(nodeHeaders[2016], 2016));

		using var fakeNode = new FakeNode(nodeHeaders);
		var requests = await fakeNode.SyncAsync(chain, settleTime: TimeSpan.FromSeconds(2));

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
		var requests = await fakeNode.SyncAsync(chain, settleTime: TimeSpan.FromSeconds(2));

		Assert.Equal(2016, chain.Tip!.Height);
		Assert.Equal(1, requests);
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

	private sealed class FakeNode : IDisposable
	{
		private readonly List<BlockHeader> _headers;
		private readonly Dictionary<uint256, int> _heights;
		private readonly NodeServer _server;
		private int _getHeadersReceived;

		public FakeNode(List<BlockHeader> headers)
		{
			_headers = headers;
			_heights = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h.GetHash(), x => x.i);
			_server = new NodeServer(Network) { LocalEndpoint = new IPEndPoint(IPAddress.Loopback, FreePort()), AllowLocalPeers = true };
			_server.InboundNodeConnectionParameters.TemplateBehaviors.Clear();
			_server.MessageReceived += OnMessageReceived;
			_server.Listen();
		}

		public async Task<int> SyncAsync(ConcurrentChain chain, TimeSpan settleTime)
		{
			using var node = await Node.ConnectAsync(Network, _server.LocalEndpoint);
			node.Behaviors.Add(new HeaderSyncBehavior(chain, new FilterHeaderChain(), new EventBus()));
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
			var start = getHeaders.BlockLocators.Blocks.Select(h => _heights.TryGetValue(h, out var height) ? height : -1).FirstOrDefault(h => h >= 0, 0) + 1;
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
