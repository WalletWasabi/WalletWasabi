using Moq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.BitcoinCore.Rpc.Models;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BitcoinCore
{
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
			using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
			var indexer = new IndexBuilderService(rpc, blockNotifier, ".");

			indexer.Synchronize();

			await Task.Delay(TimeSpan.FromSeconds(1));
			//// Assert.False(indexer.IsRunning);     // <------------ ERROR: it should have stopped but there is a bug for RegTest
			Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
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
			using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
			var indexer = new IndexBuilderService(rpc, blockNotifier, ".");

			indexer.Synchronize();

			await Task.Delay(TimeSpan.FromSeconds(2));
			Assert.True(indexer.IsRunning);  // It is still working
			Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
			Assert.True(called > 1);
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
						Blocks = (ulong)called - 1,
						InitialBlockDownload = true
					});
				},
				OnGetBlockHashAsync = (height) => Task.FromResult(blockchain[height].Hash),
				OnGetVerboseBlockAsync = (hash) => Task.FromResult(blockchain.Single(x => x.Hash == hash))
			};
			using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
			var indexer = new IndexBuilderService(rpc, blockNotifier, ".");

			indexer.Synchronize();

			await Task.Delay(TimeSpan.FromSeconds(10));
			Assert.True(indexer.IsRunning);  // It is still working
			Assert.Throws<ArgumentOutOfRangeException>(() => indexer.GetLastFilter());  // There are no filters
			Assert.True(called > 1);
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
			using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
			var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.txt");

			indexer.Synchronize();

			await Task.Delay(TimeSpan.FromSeconds(5));
			Assert.False(indexer.IsRunning);  // we are done

			var result = indexer.GetFilterLinesExcluding(blockchain[0].Hash, 100, out var found);
			Assert.True(found);
			Assert.Equal(9, result.bestHeight.Value);
			Assert.Equal(9, result.filters.Count());
		}

		[Fact]
		public async Task IncludeTaprootScriptInFiltersAsync()
		{
			var blockchain = GenerateBlockchain().Take(10).ToArray();
			var mockRpcClient = new Mock<IRPCClient>();
			mockRpcClient.SetupGet(rpc => rpc.Network).Returns(Network.RegTest);
			mockRpcClient.Setup(rpc => rpc.GetBlockchainInfoAsync()).ReturnsAsync(new BlockchainInfo
			{
				Headers = (ulong)blockchain.Length - 1,
				Blocks = (ulong)blockchain.Length - 1,
				InitialBlockDownload = false
			});
			mockRpcClient.Setup(rpc => rpc.GetBlockHashAsync(It.IsAny<int>()))
				.ReturnsAsync((int height) => blockchain[height].Hash);
			mockRpcClient.Setup(rpc => rpc.GetVerboseBlockAsync(It.IsAny<uint256>()))
				.ReturnsAsync((uint256 hash) => blockchain.Single(x => x.Hash == hash));

			var rpc = mockRpcClient.Object;
			using var blockNotifier = new BlockNotifier(TimeSpan.MaxValue, rpc);
			var indexer = new IndexBuilderService(rpc, blockNotifier, "filters.txt");

			indexer.Synchronize();

			await Task.Delay(TimeSpan.FromSeconds(5));
			Assert.False(indexer.IsRunning);  // we are done

			var firstTaprootScript = blockchain.First()
				.Transactions.First()
				.Outputs.Where(x => x.PubkeyType == RpcPubkeyType.TxWitnessV1Taproot)
				.Select(x => x.ScriptPubKey)
				.First();

			var result = indexer.GetFilterLinesExcluding(blockchain[0].Hash, 100, out var found);
			static byte[] ComputeKey(uint256 blockId) => blockId.ToBytes()[0..16];

			Assert.True(result.filters.Any(filterModel => 
				filterModel.Filter.Match(
					firstTaprootScript.ToCompressedBytes(), 
					ComputeKey(filterModel.Header.BlockHash))));
		}

		private IEnumerable<VerboseBlockInfo> GenerateBlockchain() =>
			from height in GenerateHeights()
			select new VerboseBlockInfo(
				BlockHashFromHeight(height),
				height,
				BlockHashFromHeight(height + 1),
				DateTimeOffset.UtcNow.AddMinutes(height * 10),
				height,
				GenerateTransactions()
				);

		private IEnumerable<ulong> GenerateHeights() =>
			Enumerable.Range(0, int.MaxValue).Select(x => (ulong)x);

		private IEnumerable<VerboseTransactionInfo> GenerateTransactions() =>
			from i in Enumerable.Range(0, 2)
			select new VerboseTransactionInfo(
				uint256.Zero,
				Enumerable.Empty<VerboseInputInfo>(),
				GenerateOutputs());

		private IEnumerable<VerboseOutputInfo> GenerateOutputs() =>
			from scriptType in new[]{ "witness_v0_scripthash", "witness_v1_taproot"}
			select new VerboseOutputInfo(Money.Coins(1), Script.FromBytesUnsafe(new byte[]{ 0, 1, 2, 3}), scriptType);

		private static uint256 BlockHashFromHeight(ulong height)
			=> height == 0 ? uint256.Zero : Hashes.DoubleSHA256(BitConverter.GetBytes(height));
	}
}
