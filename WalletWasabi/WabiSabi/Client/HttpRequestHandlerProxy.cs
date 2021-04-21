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

		public Task<InputsRegistrationResponse> RegisterInputAsync(InputsRegistrationRequest request) =>
			SendAndReceiveAsync<InputsRegistrationRequest, InputsRegistrationResponse>("register-input", request);

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request) =>
			SendAndReceiveAsync<ConnectionConfirmationRequest, ConnectionConfirmationResponse>("confirm-connection", request);

		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request) =>
			SendAndReceiveAsync<OutputRegistrationRequest, OutputRegistrationResponse>("register-output", request);

		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request) =>
			SendAndReceiveAsync<ReissueCredentialRequest, ReissueCredentialResponse>("reissue-credential", request);

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

		private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(string actionName, TRequest request) where TRequest : class
		{
			using var content = Serialize(request);
			using var response = await _client.SendAsync(HttpMethod.Post, GetUriEndPoint(actionName), content).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			return await response.Content.ReadAsJsonAsync<TResponse>().ConfigureAwait(false);
		}

		private static string GetUriEndPoint(string action) =>
			action switch
			{
				"register-input" => $"/api/v2/btc/wabisabicoinjoin/inputs/",
				"register-output" => $"/api/v2/btc/wabisabicoinjoin/outputs/",
				"reissue-credential" => $"/api/v2/btc/wabisabicoinjoin/credentials/",
				"confirm-connection" => $"/api/v2/btc/wabisabicoinjoin/connections/",
				_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
			};
	}
}