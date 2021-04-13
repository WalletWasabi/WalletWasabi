using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class HttpRequestHandlerProxy : IArenaRequestHandler
	{
		private IHttpClient _client;

		public HttpRequestHandlerProxy(IHttpClient client)
		{
			_client = client;
		}

		public async Task<InputsRegistrationResponse> RegisterInputAsync(InputsRegistrationRequest request)
		{
			using var content = Serialize(request);
			using var response = await _client.SendAsync(HttpMethod.Post, "register-input", content).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			return await response.Content.ReadAsJsonAsync<InputsRegistrationResponse>().ConfigureAwait(false);
		}

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request)
		{
			throw new NotImplementedException();
		}

		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request)
		{
			throw new NotImplementedException();
		}

		public Task RemoveInputAsync(InputsRemovalRequest request)
		{
			throw new NotImplementedException();
		}

		public Task SignTransactionAsync(TransactionSignaturesRequest request)
		{
			throw new NotImplementedException();
		}

		private static StringContent Serialize(object obj)
		{
			string jsonString = JsonConvert.SerializeObject(obj, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}

		private static string GetUriEndPoint(string action) =>
			action switch
			{
				"register-input" => $"/api/v2/btc/wabisabicoinjoin/inputs/",
				_ => $"/api/v2/btc/wabisabicoinjoin/who-knows/"
			};

	}
}