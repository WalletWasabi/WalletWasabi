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

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType network)
		{
			using (var client = new WasabiClient(_networkUriMappings[network]))
			{
				var feeEstimationPairs = await client.GetFeesAsync(1000);

				Assert.NotNull(feeEstimationPairs);
				Assert.NotEmpty(feeEstimationPairs);
			}
		}

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFiltersAsync(NetworkType network)
		{
			using (var client = new WasabiClient(_networkUriMappings[network]))
			{
				var filterModel = IndexDownloader.GetStartingFilter(Network.GetNetwork(network.ToString()));

				var filters = await client.GetFiltersAsync(filterModel.BlockHash, 2);

				Assert.True(filters.NotNullAndNotEmpty());
				Assert.True(filters.Count() == 2);
			}
		}
	}
}
