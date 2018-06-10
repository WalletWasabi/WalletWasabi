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

namespace WalletWasabi.Tests {
	public class LiveServerTests : IClassFixture<SharedFixture> {
		public Dictionary<Network, Uri> NetworkUriMappings;
		public LiveServerTests()
		{
			NetworkUriMappings = new Dictionary<Network, Uri>
			{
				{ Network.Main, new Uri("http://wtgjmaol3io5ijii.onion") },
				{ Network.TestNet, new Uri("http://4jsmnfcsmbrlm7l7.onion") }
			};
		}
		[Fact]
		public async Task GetFeesAsync()
		{
			foreach (var server in NetworkUriMappings)
			{
				try
				{
					Logger.LogInfo<LiveServerTests>($"Init client for {server.Key}");
					using (var client = new WasabiClient(server.Value, null))
					{
						var feeEstimationPairs = await client.GetFeesAsync(1000);
						Assert.NotNull(feeEstimationPairs);
						Assert.NotEmpty(feeEstimationPairs);
						Logger.LogInfo<LiveServerTests>($"GetFeesAsync successful for {server.Key}");
					}
				}
				catch (Exception ex)
				{
					
					throw ex;
				}
			}
		}
	}
}