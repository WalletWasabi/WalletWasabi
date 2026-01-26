using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using static WalletWasabi.Services.Workers;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class ReorgTest : IClassFixture<RegTestFixture>
{
	public ReorgTest(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	private async Task WaitForIndexesToSyncAsync(TimeSpan timeout, BitcoinStore bitcoinStore)
	{
		var bestHash = await RegTestFixture.IndexerRegTestNode.RpcClient.GetBestBlockHashAsync();

		var times = 0;
		while (bitcoinStore.SmartHeaderChain.TipHash != bestHash)
		{
			if (times > timeout.TotalSeconds)
			{
				throw new TimeoutException($"{nameof(Synchronizer)} test timed out. Filter was not downloaded.");
			}

			await Task.Delay(TimeSpan.FromSeconds(1));
			times++;
		}
	}

	[Fact]
	public async Task ReorgTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;
		Network network = setup.Network;
		BitcoinStore bitcoinStore = setup.BitcoinStore;

		var keyManager = KeyManager.CreateNew(out _, setup.Password, network);

		// Mine some coins, make a few bech32 transactions then make it confirm.
		await rpc.GenerateAsync(1);
		var key = keyManager.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		var tx2 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
		key = keyManager.GenerateNewKey(LabelsArray.Empty, KeyState.Clean, isInternal: false);
		var tx3 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m));
		var tx4 = await rpc.SendToAddressAsync(key.PubKey.GetAddress(ScriptPubKeyType.Legacy, network),
			Money.Coins(0.1m));
		var tx5 = await rpc.SendToAddressAsync(key.PubKey.GetAddress(ScriptPubKeyType.SegwitP2SH, network),
			Money.Coins(0.1m));
		var tx1 = await rpc.SendToAddressAsync(key.GetP2wpkhAddress(network), Money.Coins(0.1m), replaceable: true);

		await rpc.GenerateAsync(2); // Generate two, so we can test for two reorg

		var filterProvider = new WebApiFilterProvider(10_000, RegTestFixture.IndexerHttpClientFactory, setup.EventBus);
		using var synchronizer = Spawn("Synchronizer", Continuously(Synchronizer.CreateFilterGenerator(filterProvider, bitcoinStore, setup.EventBus)));

		var reorgAwaiter = new EventsAwaiter<FilterModel>(
			h => bitcoinStore.FilterStore.Reorged += h,
			h => bitcoinStore.FilterStore.Reorged -= h,
			2);

		// Test initial synchronization.
		await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), bitcoinStore);

		var tip = await rpc.GetBestBlockHashAsync();
		Assert.Equal(tip, bitcoinStore.SmartHeaderChain.TipHash);
		var tipBlock = await rpc.GetBlockHeaderAsync(tip);
		Assert.Equal(tipBlock.HashPrevBlock,
			bitcoinStore.SmartHeaderChain.GetChain().Select(x => x.header.BlockHash).ToArray()[
				bitcoinStore.SmartHeaderChain.HashCount - 2]);

		// Test synchronization after fork.
		await rpc.InvalidateBlockAsync(tip); // Reorg 1
		tip = await rpc.GetBestBlockHashAsync();
		await rpc.InvalidateBlockAsync(tip); // Reorg 2
		var tx1bumpRes = await rpc.BumpFeeAsync(tx1); // RBF it

		await rpc.GenerateAsync(5);
		await WaitForIndexesToSyncAsync(TimeSpan.FromSeconds(90), bitcoinStore);

		var hashes = bitcoinStore.SmartHeaderChain.GetChain().Select(x => x.header.BlockHash).ToArray();
		Assert.DoesNotContain(tip, hashes);
		Assert.DoesNotContain(tipBlock.HashPrevBlock, hashes);

		tip = await rpc.GetBestBlockHashAsync();
		Assert.Equal(tip, bitcoinStore.SmartHeaderChain.TipHash);

		FilterModel[] filters =
			await bitcoinStore.FilterStore.FetchBatchAsync(fromHeight: 0, batchSize: -1, testDeadlineCts.Token);
		var filterTip = filters.Last();
		Assert.Equal(tip, filterTip.Header.BlockHash);

		// Test filter block hashes are correct after fork.
		var blockCountIncludingGenesis = await rpc.GetBlockCountAsync() + 1;

		for (int i = 0; i < blockCountIncludingGenesis; i++)
		{
			var expectedHash = await rpc.GetBlockHashAsync(i);
			var filter = filters[i];
			Assert.Equal(i, (int) filter.Header.Height);
			Assert.Equal(expectedHash, filter.Header.BlockHash);
			if (i < 101) // Later other tests may fill the filter.
			{
				Assert.Equal(LegacyWasabiFilterGenerator.CreateDummyEmptyFilter(expectedHash).ToString(),
					filter.Filter.ToString());
			}
		}

		// Test the serialization, too.
		tip = await rpc.GetBestBlockHashAsync();
		var blockHash = tip;
		for (var i = 0; i < hashes.Length; i++)
		{
			var block = await rpc.GetBlockHeaderAsync(blockHash);
			Assert.Equal(blockHash, hashes[hashes.Length - i - 1]);
			blockHash = block.HashPrevBlock;
		}

		// Assert reorg happened exactly as many times as we reorged.
		await reorgAwaiter.WaitAsync(TimeSpan.FromSeconds(10));
	}
}
