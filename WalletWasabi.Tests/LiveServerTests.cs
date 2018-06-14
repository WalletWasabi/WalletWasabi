using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Services;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests
{
	public class LiveServerTests : IClassFixture<SharedFixture>
	{
		private readonly Dictionary<NetworkType, Uri> _networkUriMappings = new Dictionary<NetworkType, Uri>
		{
				{ NetworkType.Mainnet, new Uri("http://4jsmnfcsmbrlm7l7.onion") },
				{ NetworkType.Testnet, new Uri("http://wtgjmaol3io5ijii.onion") }
		};

		// Blockchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
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
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
			{
				var filterModel = IndexDownloader.GetStartingFilter(Network.GetNetwork(networkType.ToString()));

				var filters = await client.GetFiltersAsync(filterModel.BlockHash, 2);

				Assert.True(filters.NotNullAndNotEmpty());
				Assert.True(filters.Count() == 2);
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetAllRoundStatesAsync(NetworkType networkType)
		{
			using (var client = new SatoshiClient(_networkUriMappings[networkType]))
			{
				var states = await client.GetAllRoundStatesAsync();
				var noRegisteredPeers = states.All(s => s.RegisteredPeerCount == 0);

				Assert.True(noRegisteredPeers);
				Assert.True(states.NotNullAndNotEmpty());
				Assert.True(states.Count() == 2);
			}
		}

		// Offchain
		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetExchangeRatesAsync(NetworkType networkType)
		{
			using (var client = new WasabiClient(_networkUriMappings[networkType]))
			{
				var exchangeRates = await client.GetExchangeRatesAsync();

				Assert.True(exchangeRates.NotNullAndNotEmpty());
			}
		}
	}
}
