using HiddenWallet.ChaumianCoinJoin.Models;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianCoinJoin
{
	public class ChaumianTumblerClient : IDisposable
	{
		private HttpClient HttpClient { get; }
		public Uri BaseAddress => HttpClient.BaseAddress;
		private readonly AsyncLock _asyncLock = new AsyncLock();

		public ChaumianTumblerClient(string address, HttpMessageHandler handler, bool disposeHandler = false)
		{
			if (address == null) throw new ArgumentNullException(nameof(address));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			HttpClient = new HttpClient(handler, disposeHandler)
			{
				BaseAddress = new Uri(address + "api/v1/Tumbler/")
			};
		}

		/// <summary>
		/// throws if unsuccessful
		/// </summary>
		public async Task TestConnectionAsync(CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				HttpResponseMessage response =
						await HttpClient.GetAsync("", HttpCompletionOption.ResponseContentRead, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				if (responseString != "test") throw new HttpRequestException($"{nameof(responseString)} = \"{responseString}\"");
			}
		}

		public async Task<StatusResponse> GetStatusAsync(CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				HttpResponseMessage response =
						await HttpClient.GetAsync("status", HttpCompletionOption.ResponseContentRead, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);

				return JsonConvert.DeserializeObject<StatusResponse>(responseString);
			}
		}

		public async Task<InputsResponse> PostInputsAsync(InputsRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("inputs", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);

				return JsonConvert.DeserializeObject<InputsResponse>(responseString);
			}
		}
		
		public async Task<ConnectionConfirmationResponse> PostConnectionConfirmationAsync(ConnectionConfirmationRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("connection-confirmation", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);

				return JsonConvert.DeserializeObject<ConnectionConfirmationResponse>(responseString);
			}
		}

		public async Task PostDisconnectionAsync(DisconnectionRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("disconnection", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);
			}
		}

		public async Task PostOutputAsync(OutputRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("output", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);
			}
		}

		public async Task<CoinJoinResponse> PostCoinJoinAsync(CoinJoinRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("coinjoin", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);

				return JsonConvert.DeserializeObject<CoinJoinResponse>(responseString);
			}
		}

		public async Task PostSignatureAsync(SignatureRequest request, CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				string requestJsonString = JsonConvert.SerializeObject(request);
				var content = new StringContent(
					requestJsonString,
					Encoding.UTF8,
					"application/json");

				HttpResponseMessage response =
						await HttpClient.PostAsync("signature", content, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);
			}
		}

		public async Task<InputRegistrationStatusResponse> GetInputRegistrationStatusAsync(CancellationToken cancel)
		{
			using (await _asyncLock.LockAsync())
			{
				HttpResponseMessage response =
						await HttpClient.GetAsync("input-registration-status", HttpCompletionOption.ResponseContentRead, cancel);

				if (!response.IsSuccessStatusCode) throw new HttpRequestException(response.StatusCode.ToString());
				string responseString = await response.Content.ReadAsStringAsync();
				AssertSuccess(responseString);

				return JsonConvert.DeserializeObject<InputRegistrationStatusResponse>(responseString);
			}
		}

		private static void AssertSuccess(string responseString)
		{
			var jObject = JObject.Parse(responseString);
			if (!jObject.Value<bool>("success"))
			{
				throw new HttpRequestException($"Message: {jObject.Value<string>("message")} Details: {jObject.Value<string>("details")}");
			}
		}

		#region IDisposable Support
		private bool _disposedValue = false; // To detect redundant calls

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

		// override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~BlockCypherClient() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}