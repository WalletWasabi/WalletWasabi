using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Tests.XunitConfiguration;
using WalletWasabi.WebClients.BlockCypher;
using WalletWasabi.WebClients.BlockCypher.Models;
using WalletWasabi.WebClients.SmartBit;
using WalletWasabi.WebClients.SmartBit.Models;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests
{
	public class ExternalApiTests
	{
		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task SmartBitTestsAsync(string networkString)
		{
			if (!await TestAsync("https://api.smartbit.com.au/v1/blockchain/stats"))
			{
				return; // If website does not work, do not bother failing.
			}

			var network = Network.GetNetwork(networkString);
			using var client = new SmartBitClient(network);
			IEnumerable<SmartBitExchangeRate> rates = rates = await client.GetExchangeRatesAsync(CancellationToken.None);

			Assert.Contains("AUD", rates.Select(x => x.Code));
			Assert.Contains("USD", rates.Select(x => x.Code));

			Logger.TurnOff();
			await Assert.ThrowsAsync<HttpRequestException>(async () => await client.PushTransactionAsync(network.Consensus.ConsensusFactory.CreateTransaction(), CancellationToken.None));
			Logger.TurnOn();
		}

		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task BlockCypherTestsAsync(string networkString)
		{
			if (!await TestAsync("https://api.blockcypher.com/v1/btc/main"))
			{
				return; // If website does not work, do not bother failing.
			}

			var network = Network.GetNetwork(networkString);
			using var client = new BlockCypherClient(network);
			BlockCypherGeneralInformation response = null;
			try
			{
				response = await client.GetGeneralInformationAsync(CancellationToken.None);
			}
			catch // stupid CI internet connection sometimes fails
			{
				await Task.Delay(3000);
				response = await client.GetGeneralInformationAsync(CancellationToken.None);
			}
			Assert.NotNull(response.Hash);
			Assert.NotNull(response.LastForkHash);
			Assert.NotNull(response.PreviousHash);
			Assert.True(response.UnconfirmedCount > 0);
			Assert.InRange(response.LowFee.FeePerK, Money.Zero, response.MediumFee.FeePerK);
			Assert.InRange(response.MediumFee.FeePerK, response.LowFee.FeePerK, response.HighFee.FeePerK);
			Assert.InRange(response.HighFee.FeePerK, response.MediumFee.FeePerK, Money.Coins(0.1m));
			Assert.True(response.Height >= 491999);
			Assert.Equal(new Uri(client.BaseAddress + "/blocks/" + response.Hash), response.LatestUrl);
			Assert.Equal(new Uri(client.BaseAddress + "/blocks/" + response.PreviousHash), response.PreviousUrl);
			if (network == Network.Main)
			{
				Assert.Equal("BTC.main", response.Name);
			}
			else
			{
				Assert.Equal("BTC.test3", response.Name);
			}
			Assert.True(response.PeerCount > 0);
		}

		private async Task<bool> TestAsync(string uri)
		{
			try
			{
				using var client = new HttpClient();
				using var res = await client.GetAsync(uri);
				if (res.StatusCode == HttpStatusCode.OK)
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogDebug($"Uri was not reachable: {uri}");
				Logger.LogDebug(ex);
			}
			return false;
		}
	}
}
