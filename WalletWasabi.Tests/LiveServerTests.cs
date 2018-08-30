using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Backend.Models.Responses;
using WalletWasabi.Crypto;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.WebClients.Wasabi.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests
{
	[Collection("LiveServerTests collection")]
	public class LiveServerTests : IClassFixture<SharedFixture>
	{
		private SharedFixture SharedFixture { get; }

		private LiveServerTestsFixture LiveServerTestsFixture { get; }

		public LiveServerTests(SharedFixture sharedFixture, LiveServerTestsFixture liveServerTestsFixture)
		{
			SharedFixture = sharedFixture;
			LiveServerTestsFixture = liveServerTestsFixture;

			var torManager = new TorProcessManager(SharedFixture.TorSocks5Endpoint);
			torManager.StartAsync().GetAwaiter().GetResult();
		}

		#region Blockchain

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
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
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				var filterModel = IndexDownloader.GetStartingFilter(Network.GetNetwork(networkType.ToString()));

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
			using (var client = new SatoshiClient(LiveServerTestsFixture.UriMappings[networkType]))
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
		public async Task GetExchangeRatesAsync(NetworkType networkType)
		{
			Console.WriteLine("public async Task GetExchangeRatesAsync(NetworkType networkType)");
			using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))
			{
				Console.WriteLine("using (var client = new WasabiClient(LiveServerTestsFixture.UriMappings[networkType]))");
				var exchangeRates = await client.GetExchangeRatesAsync().ConfigureAwait(false);
				Console.WriteLine("var exchangeRates = await client.GetExchangeRatesAsync();");

				Assert.True(exchangeRates.NotNullAndNotEmpty());
				Console.WriteLine("Assert.True(exchangeRates.NotNullAndNotEmpty());");
			}
		}

		#endregion Offchain
	}
}
