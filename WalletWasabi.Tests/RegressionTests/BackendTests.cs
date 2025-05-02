using NBitcoin;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Serialization;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.Wasabi;
using Xunit;
using Constants = WalletWasabi.Helpers.Constants;

namespace WalletWasabi.Tests.RegressionTests;

/// <seealso cref="RegTestCollectionDefinition"/>
[Collection("RegTest collection")]
public class BackendTests : IClassFixture<RegTestFixture>
{
	public BackendTests(RegTestFixture regTestFixture)
	{
		RegTestFixture = regTestFixture;
		BackendApiHttpClient = regTestFixture.IndexerHttpClientFactory.CreateClient("test");
	}

	private HttpClient BackendApiHttpClient { get; }
	private RegTestFixture RegTestFixture { get; }

	[Fact]
	public async Task GetClientVersionAsync()
	{
		IndexerClient client = new(BackendApiHttpClient);
		var backendCompatible = await client.CheckUpdatesAsync(CancellationToken.None);
		Assert.True(backendCompatible);
	}

	[Fact]
	public async Task BroadcastReplayTxAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;

		var utxos = await rpc.ListUnspentAsync();
		var utxo = utxos[0];
		var tx = await rpc.GetRawTransactionAsync(utxo.OutPoint.Hash);
		using StringContent content = new($"'{tx.ToHex()}'", Encoding.UTF8, "application/json");

		Logger.TurnOff();

		using var response = await BackendApiHttpClient.PostAsync($"api/v{Constants.BackendMajorVersion}/btc/blockchain/broadcast", content);
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("Transaction is already in the blockchain.", await response.Content.ReadAsJsonAsync(Decode.String));

		Logger.TurnOn();
	}

	[Fact]
	public async Task BroadcastInvalidTxAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);

		using StringContent content = new($"''", Encoding.UTF8, "application/json");

		Logger.TurnOff();

		using var response = await BackendApiHttpClient.PostAsync($"api/v{Constants.BackendMajorVersion}/btc/blockchain/broadcast", content);

		Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("The hex field is required.", await response.Content.ReadAsStringAsync());

		Logger.TurnOn();
	}

	[Fact]
	public async Task FilterBuilderTestAsync()
	{
		await using RegTestSetup setup = await RegTestSetup.InitializeTestEnvironmentAsync(RegTestFixture);
		IRPCClient rpc = setup.RpcClient;
		IndexBuilderService indexBuilderService = new(rpc, "filters.txt");
		var startIndexingService = indexBuilderService.StartAsync(CancellationToken.None);
		try
		{
			// Test initial synchronization.
			var times = 0;
			uint256 firstHash = await rpc.GetBlockHashAsync(0);
			while ((await indexBuilderService.GetFilterLinesExcludingAsync(firstHash, 101)).filters.Count() != 101)
			{
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
				}
				await Task.Delay(100);
				times++;
			}

			// Test later synchronization.
			await rpc.GenerateAsync(10);
			times = 0;
			var filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(firstHash, 111);
			while (filterResponse.filters.Count() != 111)
			{
				Assert.True(filterResponse.found);
				if (times > 500) // 30 sec
				{
					throw new TimeoutException($"{nameof(IndexBuilderService)} test timed out.");
				}
				await Task.Delay(100);
				times++;
				filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(firstHash, 111);
			}

			// Test correct number of filters is received.
			var hundredthHash = await rpc.GetBlockHashAsync(100);
			filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(hundredthHash, 11);
			Assert.Equal(11, filterResponse.filters.Count());
			Assert.True(filterResponse.found);
			var bestHash = await rpc.GetBestBlockHashAsync();

			filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(bestHash, 1);
			Assert.Empty(filterResponse.filters);

			filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(uint256.Zero, 1);
			Assert.Empty(filterResponse.filters);
			Assert.False(filterResponse.found);

			// Test filter block hashes are correct.
			filterResponse = await indexBuilderService.GetFilterLinesExcludingAsync(firstHash, 111);
			Assert.True(filterResponse.found);
			var filters = filterResponse.filters.ToArray();
			for (int i = 0; i < 111; i++)
			{
				var expectedHash = await rpc.GetBlockHashAsync(i + 1);
				var filterModel = filters[i];
				Assert.Equal(expectedHash, filterModel.Header.BlockHash);
			}
		}
		finally
		{
			await startIndexingService;
			await indexBuilderService.StopAsync(CancellationToken.None);
		}
	}
}
