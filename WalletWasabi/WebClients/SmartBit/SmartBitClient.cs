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
	public class SmartBitClient : IDisposable
	{
		private bool _disposedValue = false; // To detect redundant calls

		public SmartBitClient(Network network, HttpMessageHandler handler = null, bool disposeHandler = false)
		{
			Network = network ?? throw new ArgumentNullException(nameof(network));
			HttpClient = handler != null
				? new HttpClient(handler, disposeHandler)
				: new HttpClient();

			if (network == Network.Main)
			{
				HttpClient.BaseAddress = new Uri("https://api.smartbit.com.au/v1/");
			}
			else if (network == Network.TestNet)
			{
				HttpClient.BaseAddress = new Uri("https://testnet-api.smartbit.com.au/v1/");
			}
			else
			{
				throw new NotSupportedNetworkException(network);
			}
		}

		private AsyncLock AsyncLock { get; } = new AsyncLock();
		private HttpClient HttpClient { get; }
		public Network Network { get; }
		public Uri BaseAddress => HttpClient.BaseAddress;

		public async Task PushTransactionAsync(Transaction transaction, CancellationToken cancel)
		{
			using (await AsyncLock.LockAsync(cancel))
			{
				using var content = new StringContent(
					new JObject(new JProperty("hex", transaction.ToHex())).ToString(),
					Encoding.UTF8,
					"application/json");

				using HttpResponseMessage response =
						await HttpClient.PostAsync("blockchain/pushtx", content, cancel);

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
				using HttpResponseMessage response =
						await HttpClient.GetAsync("exchange-rates", HttpCompletionOption.ResponseContentRead, cancel);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					throw new HttpRequestException(response.StatusCode.ToString());
				}

				using var responseContent = response.Content;
				string responseString = await responseContent.ReadAsStringAsync();
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

		#region IDisposable Support

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					// dispose managed state (managed objects).
					HttpClient?.Dispose();
				}

				// free unmanaged resources (unmanaged objects) and override a finalizer below.
				// set large fields to null.

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}

		#endregion IDisposable Support
	}
}
