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

		private enum RemoteAction
		{
			RegisterInput,
			RemoveInput,
			ConfirmConnection,
			RegisterOutput,
			ReissueCredential,
			SignTransaction
		}
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request) =>
			SendAndReceiveAsync<InputRegistrationRequest, InputRegistrationResponse>(RemoteAction.RegisterInput, request);

		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request) =>
			SendAndReceiveAsync<ConnectionConfirmationRequest, ConnectionConfirmationResponse>(RemoteAction.ConfirmConnection, request);

		public Task<OutputRegistrationResponse> RegisterOutputAsync(OutputRegistrationRequest request) =>
			SendAndReceiveAsync<OutputRegistrationRequest, OutputRegistrationResponse>(RemoteAction.RegisterOutput, request);

		public Task<ReissueCredentialResponse> ReissueCredentialAsync(ReissueCredentialRequest request) =>
			SendAndReceiveAsync<ReissueCredentialRequest, ReissueCredentialResponse>(RemoteAction.ReissueCredential, request);

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

		private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request) where TRequest : class
		{
			using var content = Serialize(request);
			using var response = await _client.SendAsync(HttpMethod.Post, GetUriEndPoint(action), content).ConfigureAwait(false);

			if (response.StatusCode != HttpStatusCode.OK)
			{
				await response.ThrowRequestExceptionFromContentAsync().ConfigureAwait(false);
			}

			return await response.Content.ReadAsJsonAsync<TResponse>().ConfigureAwait(false);
		}

		private static string GetUriEndPoint(RemoteAction action) =>
			action switch
			{
				_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
			};
	}
}