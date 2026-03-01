using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Serialization;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client;

public class WabiSabiHttpApiClient : IWabiSabiApiRequestHandler
{
	private readonly string _identity;
	private readonly IHttpClientFactory _httpClientFactory;

	public WabiSabiHttpApiClient(string identity, IHttpClientFactory httpClientFactory)
	{
		_identity = identity;
		_httpClientFactory = httpClientFactory;
	}

	private enum RemoteAction
	{
		RegisterInput,
		RemoveInput,
		ConfirmConnection,
		RegisterOutput,
		ReissueCredential,
		SignTransaction,
		GetStatus,
		ReadyToSign
	}

	public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<InputRegistrationRequest, InputRegistrationResponse>(RemoteAction.RegisterInput, request, cancellationToken);

	public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ConnectionConfirmationRequest, ConnectionConfirmationResponse>(RemoteAction.ConfirmConnection, request, cancellationToken);

	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.RegisterOutput, request, cancellationToken);

	public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ReissueCredentialRequest, ReissueCredentialResponse>(RemoteAction.ReissueCredential, request, cancellationToken);

	public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.RemoveInput, request, cancellationToken);

	public virtual Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.SignTransaction, request, cancellationToken);

	public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<RoundStateRequest, RoundStateResponse>(RemoteAction.GetStatus, request, cancellationToken);

	public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.ReadyToSign, request, cancellationToken);

	private async Task<HttpResponseMessage> InternalSendAsync(RemoteAction action, string jsonString, CancellationToken cancellationToken)
	{
		var httpClient = _httpClientFactory.CreateClient(_identity);
		using StringContent content = new(jsonString, Encoding.UTF8, MediaTypeNames.Application.Json);
		return await httpClient.PostAsync(GetUriEndPoint(action), content, cancellationToken).ConfigureAwait(false);
	}

	private async Task<string> InternalSendAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		var jsonRequest = JsonEncoder.ToString(request, Encode.CoordinatorMessage);
		using var response = await InternalSendAsync(action, jsonRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowUnwrapExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task SendAndReceiveAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		await InternalSendAsync(action, request, cancellationToken).ConfigureAwait(false);
	}

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		var jsonString = await InternalSendAsync(action, request, cancellationToken).ConfigureAwait(false);
		return Decode.CoordinatorMessage<TResponse>(jsonString);
	}

	private static string GetUriEndPoint(RemoteAction action) =>
		"wabisabi/" + action switch
		{
			RemoteAction.RegisterInput => "input-registration",
			RemoteAction.RegisterOutput => "output-registration",
			RemoteAction.ConfirmConnection => "connection-confirmation",
			RemoteAction.ReissueCredential => "credential-issuance",
			RemoteAction.RemoveInput => "input-unregistration",
			RemoteAction.SignTransaction => "transaction-signature",
			RemoteAction.GetStatus => "status",
			RemoteAction.ReadyToSign => "ready-to-sign",
			_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
		};
}
