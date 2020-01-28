using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Exceptions;
using WalletWasabi.WebClients.SmartBit.Models;

namespace WalletWasabi.WebClients.SmartBit
{
	public class SmartBitClient
	{
		public Network Network { get; }
		public Uri BaseAddress { get; }
		private AsyncLock AsyncLock { get; } = new AsyncLock();

		public SmartBitClient(Network network)
		{
			Network = network ?? throw new ArgumentNullException(nameof(network));

			if (network == Network.Main)
			{
				BaseAddress = new Uri("https://api.smartbit.com.au/v1/");
			}
			else if (network == Network.TestNet)
			{
				BaseAddress = new Uri("https://testnet-api.smartbit.com.au/v1/");
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		public async Task PushTransactionAsync(Transaction transaction, CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel))
			{
				using var httpClient = CreateHttpClient();
				
				using var content = new StringContent(
					new JObject(new JProperty("hex", transaction.ToHex())).ToString(),
					Encoding.UTF8,
					"application/json");

				using HttpResponseMessage response =
						await httpClient.PostAsync("blockchain/pushtx", content, cancel);

				if (response.StatusCode != HttpStatusCode.OK)
				{
					throw new HttpRequestException(response.StatusCode.ToString());
				}

				using var responseContent = response.Content;
				string responseString = await responseContent.ReadAsStringAsync();
				AssertSuccess(responseString);
			}
		}

		public async Task<IEnumerable<SmartBitExchangeRate>> GetExchangeRatesAsync(CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel))
			{
				using var httpClient = CreateHttpClient();

				using var response = await httpClient.GetAsync("exchange-rates", HttpCompletionOption.ResponseContentRead, cancel);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					throw new HttpRequestException(response.StatusCode.ToString());
				}

				using var content = response.Content;
				string responseString = await content.ReadAsStringAsync();
				AssertSuccess(responseString);

				var exchangeRates = JObject.Parse(responseString).Value<JArray>("exchange_rates");
				var ret = new HashSet<SmartBitExchangeRate>();
				foreach (var jtoken in exchangeRates)
				{
					ret.Add(JsonConvert.DeserializeObject<SmartBitExchangeRate>(jtoken.ToString()));
				}
				return ret;
			}
		}

		private static void AssertSuccess(string responseString)
		{
			var jObject = JObject.Parse(responseString);
			if (!jObject.Value<bool>("success"))
			{
				throw new HttpRequestException($"Error code: {jObject["error"].Value<string>("code")} Reason: {jObject["error"].Value<string>("message")}");
			}
		}

		private HttpClient CreateHttpClient()
		{
			return new HttpClient()
			{
				BaseAddress = BaseAddress
			};
		}
	}
}
