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
