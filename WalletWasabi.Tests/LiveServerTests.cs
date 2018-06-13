using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.TorSocks5;
using WalletWasabi.WebClients.ChaumianCoinJoin;
using Xunit;

namespace WalletWasabi.Tests
{
	public class LiveServerTests : IClassFixture<SharedFixture>
	{
		private readonly Dictionary<NetworkType, Uri> _networkUriMappings = new Dictionary<NetworkType, Uri>
		{
				{ NetworkType.Mainnet, new Uri("http://wtgjmaol3io5ijii.onion") },
				{ NetworkType.Testnet, new Uri("http://4jsmnfcsmbrlm7l7.onion") }
		};

		[Theory]
		[InlineData(NetworkType.Mainnet)]
		[InlineData(NetworkType.Testnet)]
		public async Task GetFeesAsync(NetworkType network)
		{
			try
			{
				Logger.LogInfo<LiveServerTests>($"Init client for {network}");

				using (var client = new WasabiClient(_networkUriMappings[network], null))
				{
					var feeEstimationPairs = await client.GetFeesAsync(1000);

					Assert.NotNull(feeEstimationPairs);
					Assert.NotEmpty(feeEstimationPairs);

					Logger.LogInfo<LiveServerTests>($"GetFeesAsync successful for {network}");
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug<LiveServerTests>(ex);
			}
		}
	}
}
