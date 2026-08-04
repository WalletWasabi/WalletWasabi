using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Blockchain.BlockFilters;

public class FilterCheckpointsTests
{
	[Fact]
	public async Task VerifyCheckPointAsync()
	{
		// A peer that provides compact block filters and blocks. Do not trust, verify.
		var nodeEndpoint = "[::ffff:89.58.60.208]:8333";
		var network = Network.Main;

		// var blockHash = uint256.Parse("000000000000000000002afe1e2f7e176047529419532b2a6773c45623a02c12");
		// var blockHeight = 940_000u;

		// var blockHash = uint256.Parse("000000000000000000010b93c9ea1c29fea277383f0f7d1f26de8b5802e885ff");
		// var blockHeight = 950_000u;

		var blockHash = uint256.Parse("000000000000000000001268aab06132c2dd203f77b6020462cd177942d6959d");
		var blockHeight = 960_000u;

		var data = await GetCheckPointDataAsync(network, nodeEndpoint, blockHash, blockHeight);

		var grFilter = new GolombRiceFilter(data.CompactFilterPayload.FilterBytes);
		var actualCompactFilterHeader = grFilter.GetHeader(data.CompactFilterHeadersPayload.PreviousFilterHeader);

		Debug.WriteLine($"Block hash:               {data.Block.GetHash()}");
		Debug.WriteLine($"Block filter header hash: {actualCompactFilterHeader}");
		Debug.WriteLine($"Block height              {blockHeight}L");
		Debug.WriteLine($"Block time:               {data.Block.Header.BlockTime.ToUnixTimeSeconds()}L");
		Debug.WriteLine($"Block filter:             {Convert.ToHexStringLower(data.CompactFilterPayload.FilterBytes)}");
		Debug.WriteLine("");
	}

	private static async Task<CheckPointData> GetCheckPointDataAsync(Network network, string endpoint, uint256 blockHash, uint blockHeight)
	{
		var node = await Node.ConnectAsync(network, endpoint);
		node.Behaviors.Add(new PingPongBehavior());
		await node.VersionHandshakeAsync().ConfigureAwait(false);

		if (!node.PeerVersion.Services.HasFlag(NodeServices.NODE_COMPACT_FILTERS))
		{
			throw new Exception("Peer does not support compact block filters");
		}

		if (!node.PeerVersion.Services.HasFlag(NodeServices.Network))
		{
			throw new Exception("Peer does not provide blocks");
		}

		Block block;

		// Get the block.
		{
			var blocks = node.GetBlocks([blockHash]);
			block = blocks.Single();

			Assert.Equal(blockHash, block.GetHash());
			Assert.True(block.CheckMerkleRoot());
		}

		CompactFilterPayload compactFilterPayload;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var tcsFilter = new TaskCompletionSource<CompactFilterPayload>();
		var tcsFilterHeader = new TaskCompletionSource<CompactFilterHeadersPayload>();

		void Listener(Node s, IncomingMessage message)
		{
			switch (message.Message.Payload)
			{
				case CompactFilterPayload cFilter:
					tcsFilter.TrySetResult(cFilter);
					break;
				case CompactFilterHeadersPayload cfHeaders:
					tcsFilterHeader.TrySetResult(cfHeaders);
					break;
			}
		}

		node.MessageReceived += Listener;

		// Request the compact filter for the block.
		{
			var payload = new GetCompactFiltersPayload(FilterType.Basic, startHeight: blockHeight, stopHash: blockHash);
			node.SendMessage(payload);

			compactFilterPayload = await tcsFilter.Task.WaitAsync(cts.Token).ConfigureAwait(false);
		}

		CompactFilterHeadersPayload compactFilterHeadersPayload;

		// Request the compact filter header for the block.
		{
			var payload = new GetCompactFilterHeadersPayload(FilterType.Basic, startHeight: blockHeight, stopHash: blockHash);
			node.SendMessage(payload);
				
			compactFilterHeadersPayload = await tcsFilterHeader.Task.WaitAsync(cts.Token).ConfigureAwait(false);
		}

		return new CheckPointData(node, compactFilterHeadersPayload, compactFilterPayload, block);
	}

	private record CheckPointData(Node Node, CompactFilterHeadersPayload CompactFilterHeadersPayload, CompactFilterPayload CompactFilterPayload, Block Block);
}
