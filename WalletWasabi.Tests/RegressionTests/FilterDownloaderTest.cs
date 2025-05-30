using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class FilterDownloaderTest : IClassFixture<RegTestFixture>
{
	public FilterDownloaderTest(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
	}

	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task FilterDownloaderTestAsync()
	{
		using CancellationTokenSource testDeadlineCts = new(TimeSpan.FromMinutes(5));

		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;
		BitcoinStore bitcoinStore = setup.BitcoinStore;

		var filterProvider = new WebApiFilterProvider(10_000, RegTestFixture.IndexerHttpClientFactory, setup.EventBus);
		using Synchronizer synchronizer = new(period: TimeSpan.FromSeconds(1), filterProvider, bitcoinStore, setup.EventBus);
		try
		{
			await synchronizer.StartAsync(CancellationToken.None);

			var blockCount = await rpc.GetBlockCountAsync() + 1; // Plus one because of the zeroth.
																 // Test initial synchronization.
			var times = 0;
			int filterCount;
			while ((filterCount = bitcoinStore.SmartHeaderChain.HashCount) < blockCount)
			{
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(Synchronizer)} test timed out. Needed filters: {blockCount}, got only: {filterCount}.");
				}
				await Task.Delay(100);
				times++;
			}

			Assert.Equal(blockCount, bitcoinStore.SmartHeaderChain.HashCount);

			// Test later synchronization.
			await RegTestFixture.IndexerRegTestNode.GenerateAsync(10);
			times = 0;
			while ((filterCount = bitcoinStore.SmartHeaderChain.HashCount) < blockCount + 10)
			{
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(Synchronizer)} test timed out. Needed filters: {blockCount + 10}, got only: {filterCount}.");
				}
				await Task.Delay(100);
				times++;
			}

			// Test correct number of filters is received.
			Assert.Equal(blockCount + 10, bitcoinStore.SmartHeaderChain.HashCount);

			// Test filter block hashes are correct.
			FilterModel[] filters = await bitcoinStore.IndexStore.FetchBatchAsync(fromHeight: 0, batchSize: -1, testDeadlineCts.Token);

			for (int i = 0; i < 101; i++)
			{
				var expectedHash = await rpc.GetBlockHashAsync(i);
				var filter = filters[i];
				Assert.Equal(i, (int)filter.Header.Height);
				Assert.Equal(expectedHash, filter.Header.BlockHash);
				Assert.Equal(LegacyWasabiFilterGenerator.CreateDummyEmptyFilter(expectedHash).ToString(), filter.Filter.ToString());
			}
		}
		finally
		{
			await synchronizer.StopAsync(CancellationToken.None);
		}
	}
}
