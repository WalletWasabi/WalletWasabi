using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
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
		SendAndReceiveAsync<InputRegistrationRequest, InputRegistrationResponse>(RemoteAction.RegisterInput, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ConnectionConfirmationRequest, ConnectionConfirmationResponse>(RemoteAction.ConfirmConnection, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.RegisterOutput, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ReissueCredentialRequest, ReissueCredentialResponse>(RemoteAction.ReissueCredential, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.RemoveInput, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public virtual Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.SignTransaction, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<RoundStateRequest, RoundStateResponse>(RemoteAction.GetStatus, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync(RemoteAction.ReadyToSign, request, cancellationToken, retryTimeout: TimeSpan.FromSeconds(30));

	private async Task<HttpResponseMessage> SendWithRetriesAsync(RemoteAction action, string jsonString, CancellationToken cancellationToken, TimeSpan? retryTimeout = null)
	{
		var start = DateTime.UtcNow;
		var totalTimeout = TimeSpan.FromMinutes(30);

		using CancellationTokenSource absoluteTimeoutCts = new(totalTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, absoluteTimeoutCts.Token);
		CancellationToken combinedToken = linkedCts.Token;
		var httpClient = _httpClientFactory.CreateClient(_identity);

		int attempt = 1;
		do
		{
			try
			{
				using StringContent content = new(jsonString, Encoding.UTF8, "application/json");

				var requestTimeout = retryTimeout ?? TimeSpan.MaxValue;
				using CancellationTokenSource requestTimeoutCts = new(requestTimeout);
				using CancellationTokenSource requestCts = CancellationTokenSource.CreateLinkedTokenSource(combinedToken, requestTimeoutCts.Token);

				// Any transport layer errors will throw an exception here.
				HttpResponseMessage response = await httpClient.PostAsync(GetUriEndPoint(action), content, requestCts.Token).ConfigureAwait(false);

				TimeSpan totalTime = DateTime.UtcNow - start;

				if (attempt > 1)
				{
					Logger.LogDebug(
						$"Received a response for {action} in {totalTime.TotalSeconds:0.##s} after {attempt} failed attempts.");
				}
				else if (action != RemoteAction.GetStatus)
				{
					Logger.LogDebug($"Received a response for {action} in {totalTime.TotalSeconds:0.##s}.");
				}

				return response;
			}
			catch (HttpRequestException e)
			{
				Logger.LogTrace($"Attempt {attempt} to perform '{action}' failed with {nameof(HttpRequestException)}: {e.Message}.");
			}
			catch (OperationCanceledException e)
			{
				Logger.LogTrace($"Attempt {attempt} to perform '{action}' failed with {nameof(OperationCanceledException)}: {e.Message}.");
			}
			catch (Exception e)
			{
				Logger.LogDebug($"Attempt {attempt} to perform '{action}' failed with exception {e}.");
				throw;
			}

			// Wait before the next try.
			await Task.Delay(250, combinedToken).ConfigureAwait(false);

			attempt++;
		}
		while (true);
	}

	private async Task<string> SendWithRetriesAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken, TimeSpan? retryTimeout = null) where TRequest : class
	{
		var jsonRequest = JsonEncoder.ToString(request, Encode.CoordinatorMessage);
		using var response = await SendWithRetriesAsync(action, jsonRequest, cancellationToken, retryTimeout).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowUnwrapExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task SendAndReceiveAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken, TimeSpan? retryTimeout = null) where TRequest : class
	{
		await SendWithRetriesAsync(action, request, cancellationToken, retryTimeout).ConfigureAwait(false);
	}

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request, CancellationToken cancellationToken, TimeSpan? retryTimeout = null) where TRequest : class
	{
		var jsonString = await SendWithRetriesAsync(action, request, cancellationToken, retryTimeout).ConfigureAwait(false);
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
