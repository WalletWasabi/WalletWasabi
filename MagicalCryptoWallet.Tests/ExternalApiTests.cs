using MagicalCryptoWallet.WebClients.BlockCypher;
using MagicalCryptoWallet.WebClients.SmartBit;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MagicalCryptoWallet.Tests
{
	public class ExternalApiTests
	{
		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task SmartBitTestsAsync(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			using (var client = new SmartBitClient(network))
			{
				var rates = (await client.GetExchangeRatesAsync(CancellationToken.None));

				Assert.Contains("AUD", rates.Select(x => x.Code));
				Assert.Contains("USD", rates.Select(x => x.Code));

				await Assert.ThrowsAsync<HttpRequestException>(async () => await client.PushTransactionAsync(new Transaction(), CancellationToken.None));
			}
		}

		[Theory]
		[InlineData("test")]
		[InlineData("main")]
		public async Task BlockCypherTestsAsync(string networkString)
		{
			var network = Network.GetNetwork(networkString);
			using (var client = new BlockCypherClient(network))
			{
				var response = await client.GetGeneralInformationAsync(CancellationToken.None);
				Assert.NotNull(response.Hash);
				Assert.NotNull(response.LastForkHash);
				Assert.NotNull(response.PreviousHash);
				Assert.True(response.UnconfirmedCount > 0);
				Assert.InRange(response.LowFee.FeePerK, Money.Zero, response.MediumFee.FeePerK);
				Assert.InRange(response.MediumFee.FeePerK, response.LowFee.FeePerK, response.HighFee.FeePerK);
				Assert.InRange(response.HighFee.FeePerK, response.MediumFee.FeePerK, new Money(0.1m, MoneyUnit.BTC));
				Assert.True(response.Height >= 491999);
				Assert.Equal(new Uri(client.BaseAddress.ToString().Replace("http", "https") + "/blocks/" + response.Hash.ToString()), response.LatestUrl);
				Assert.Equal(new Uri(client.BaseAddress.ToString().Replace("http", "https") + "/blocks/" + response.PreviousHash.ToString()), response.PreviousUrl);
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
		}
	}
}
