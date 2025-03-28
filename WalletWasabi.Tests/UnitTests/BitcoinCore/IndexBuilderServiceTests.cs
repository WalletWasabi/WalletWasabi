using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.BitcoinRpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore;

public class IndexBuilderServiceTests
{
	[Fact]
	public async Task UnsynchronizedBitcoinNodeAsync()
	{
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = 0,
				Blocks = 0,
				InitialBlockDownload = false
			}),
		};
		using var indexer = new IndexBuilderService(rpc, "filters.sqlite");
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(1));
		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task StalledBitcoinNodeAsync()
	{
		var called = 0;
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = 10_000,
					Blocks = 0,
					InitialBlockDownload = true
				});
			}
		};
		using var indexer = new IndexBuilderService(rpc, "filters.sqlite");
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(1));

		// There is only starting filter
		Assert.True(indexer.GetLastFilter()?.Header.BlockHash.Equals(StartingFilters.GetStartingFilter(rpc.Network).Header.BlockHash));
		Assert.Equal(1, called);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public async Task SynchronizingBitcoinNodeAsync()
	{
		var called = 0;
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () =>
			{
				called++;
				return Task.FromResult(new BlockchainInfo
				{
					Headers = (ulong)blockchain.Length,
					Blocks = (ulong)called,
					InitialBlockDownload = true
				});
			},
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};
		using var indexer = new IndexBuilderService(rpc, "filters.txt");
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(1));

		var lastFilter = indexer.GetLastFilter();
		Assert.Equal(9, (int)lastFilter!.Header.Height);
		Assert.True(called > 1);

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	[Fact]
	public void IncludeTaprootScriptInFilters()
	{
		var getBlockRpcRawResponse = File.ReadAllText("./UnitTests/Data/VerboseBlock.json");

		var block = RpcParser.ParseVerboseBlockResponse(getBlockRpcRawResponse);
		var filter = IndexBuilderService.BuildFilterForBlock(block);

		var txOutputs = block.Transactions.SelectMany(x => x.Outputs);
		var prevTxOutputs = block.Transactions.SelectMany(x => x.Inputs.OfType<VerboseInputInfo.Full>().Select(y => y.PrevOut));
		var allOutputs = txOutputs.Concat(prevTxOutputs);

		var indexableOutputs = allOutputs.Where(x => x?.PubkeyType is RpcPubkeyType.TxWitnessV0Keyhash or RpcPubkeyType.TxWitnessV1Taproot);
		var nonIndexableOutputs = allOutputs.Except(indexableOutputs);

		static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

		Assert.All(indexableOutputs, x => Assert.True(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
		Assert.All(nonIndexableOutputs, x => Assert.False(filter.Match(x?.ScriptPubKey.ToCompressedBytes(), ComputeKey(block.Hash))));
	}

	[Fact]
	public async Task SynchronizedBitcoinNodeAsync()
	{
		var blockchain = GenerateBlockchain().Take(10).ToArray();
		var rpc = new MockRpcClient
		{
			OnGetBlockchainInfoAsync = () => Task.FromResult(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			}),
			OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
			OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
		};

		using var indexer = new IndexBuilderService(rpc, "filters.txt");
		var indexingStartTask = indexer.StartAsync(CancellationToken.None);

		await Task.Delay(TimeSpan.FromSeconds(1));

		var result = await indexer.GetFilterLinesExcludingAsync(blockchain[0].Hash, 100);
		Assert.True(result.found);
		Assert.Equal(9, result.bestHeight.Value);
		Assert.Equal(9, result.filters.Count());

		var indexingStopTask = indexer.StopAsync(CancellationToken.None);
		await Task.WhenAll(indexingStartTask, indexingStopTask);
	}

	private IEnumerable<VerboseBlockInfo> GenerateBlockchain() =>
		from height in Enumerable.Range(0, int.MaxValue).Select(x => (ulong)x)
		select new VerboseBlockInfo(
			BlockHashFromHeight(height),
			height,
			BlockHashFromHeight(height + 1),
			DateTimeOffset.UtcNow.AddMinutes(height * 10),
			height,
			Enumerable.Empty<VerboseTransactionInfo>());

	private static uint256 BlockHashFromHeight(ulong height)
		=> height == 0 ? uint256.Zero : Hashes.DoubleSHA256(BitConverter.GetBytes(height));
}
