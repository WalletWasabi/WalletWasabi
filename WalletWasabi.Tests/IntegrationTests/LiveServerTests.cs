using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	[Collection("LiveServerTests collection")]
	public class LiveServerTests
	{
		private LiveServerTestsFixture LiveServerTestsFixture { get; }

		public LiveServerTests(LiveServerTestsFixture liveServerTestsFixture)
		{
			LiveServerTestsFixture = liveServerTestsFixture;

			var torManager = new TorProcessManager(Global.Instance.TorSocks5Endpoint, Global.Instance.TorLogsFile);
			torManager.Start(ensureRunning: true, dataDir: Path.GetFullPath(AppContext.BaseDirectory));
			Task.Delay(3000).GetAwaiter().GetResult();
		}

		#region Blockchain

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType], Global.Instance.TorSocks5Endpoint))
			{
				var feeEstimationPairs = await client.GetFeesAsync(1000);

				Assert.True(feeEstimationPairs.NotNullAndNotEmpty());
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFiltersAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType], Global.Instance.TorSocks5Endpoint))
			{
				var filterModel = StartingFilters.GetStartingFilter(Network.GetNetwork(networkType.ToString()));

				FiltersResponse filtersResponse = await client.GetFiltersAsync(filterModel.BlockHash, 2);

				Assert.NotNull(filtersResponse);
				Assert.True(filtersResponse.Filters.Count() == 2);
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetAllRoundStatesAsync(NetworkType networkType)
		{
			using (var client = new SatoshiClient(LiveServerTestsFixture.UriMappings[networkType], Global.Instance.TorSocks5Endpoint))
			{
				var states = await client.GetAllRoundStatesAsync();
				Assert.True(states.NotNullAndNotEmpty());
				Assert.True(states.Count() >= 1);
			}
		}

		#endregion Blockchain

		#region Offchain

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetExchangeRateAsync(NetworkType networkType) // xunit wtf: If this function is called GetExchangeRatesAsync, it'll stuck on 1 CPU VMs (Manjuro, Fedora)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType], Global.Instance.TorSocks5Endpoint))
			{
				var exchangeRates = await client.GetExchangeRatesAsync();

				Assert.True(exchangeRates.NotNullAndNotEmpty());
			}
		}

		#endregion Offchain
	}
}
