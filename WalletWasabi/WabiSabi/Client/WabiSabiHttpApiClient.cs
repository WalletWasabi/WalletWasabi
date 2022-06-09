using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.WabiSabi.Client;

public class WabiSabiHttpApiClient : IWabiSabiApiRequestHandler
{
	private const int MaxRetries = 20;

	private IHttpClient _client;

	public WabiSabiHttpApiClient(IHttpClient client)
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
		SignTransaction,
		GetStatus,
		ReadyToSign
	}

	public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<InputRegistrationRequest, InputRegistrationResponse>(RemoteAction.RegisterInput, request, cancellationToken);

	public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ConnectionConfirmationRequest, ConnectionConfirmationResponse>(RemoteAction.ConfirmConnection, request, cancellationToken);

	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OutputRegistrationRequest>(RemoteAction.RegisterOutput, request, cancellationToken);

	public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ReissueCredentialRequest, ReissueCredentialResponse>(RemoteAction.ReissueCredential, request, cancellationToken);

	public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<InputsRemovalRequest>(RemoteAction.RemoveInput, request, cancellationToken);

	public virtual Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<TransactionSignaturesRequest>(RemoteAction.SignTransaction, request, cancellationToken);

	public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<RoundStateRequest, RoundStateResponse>(RemoteAction.GetStatus, request, cancellationToken);

	public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ReadyToSignRequestRequest>(RemoteAction.ReadyToSign, request, cancellationToken);

	private async Task<HttpResponseMessage> SendWithRetriesAsync(RemoteAction action, string jsonString, CancellationToken cancellationToken)
	{
		var exceptions = new List<Exception>();

		var start = DateTime.Now;

		for (var attempt = 0; attempt < MaxRetries; attempt++)
		{
			try
			{
				using StringContent content = new(jsonString, Encoding.UTF8, "application/json");

				// Any transport layer errors will throw an exception here.
				var response = await _client.SendAsync(HttpMethod.Post, GetUriEndPoint(action), content, cancellationToken).ConfigureAwait(false);

				var totalTime = DateTime.Now - start;

				if (exceptions.Any())
				{
					Logger.LogDebug($"Received a response for {action} in {totalTime.TotalSeconds:0.##s} after {attempt} failed attempts: {new AggregateException(exceptions)}.");
				}
				else
				{
					if (action != RemoteAction.GetStatus)
					{
						Logger.LogDebug($"Received a response for {action} in {totalTime.TotalSeconds:0.##s}.");
					}
				}

				return response;
			}
			catch (HttpRequestException e)
			{
				exceptions.Add(e);
			}
			catch (Exception e)
			{
				if (exceptions.Any())
				{
					exceptions.Add(e);
					throw new AggregateException(exceptions);
				}
				else
				{
					throw;
				}
			}

			// Wait before the next try.
			await Task.Delay(250, cancellationToken).ConfigureAwait(false);
		}

		throw new AggregateException(exceptions);
	}

	private async Task<string> SendWithRetriesAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		using var response = await SendWithRetriesAsync(action, Serialize(request), cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowUnwrapExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task SendAndReceiveAsync<TRequest>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		await SendWithRetriesAsync(action, request, cancellationToken).ConfigureAwait(false);
	}

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request, CancellationToken cancellationToken) where TRequest : class
	{
		var jsonString = await SendWithRetriesAsync(action, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private static string Serialize<T>(T obj)
		=> JsonConvert.SerializeObject(obj, JsonSerializationOptions.Default.Settings);

	private static TResponse Deserialize<TResponse>(string jsonString)
	{
		try
		{
			return JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings)
				?? throw new InvalidOperationException("Deserialization error");
		}
		catch
		{
			Logger.LogDebug($"Failed to deserialize {typeof(TResponse)} from JSON '{jsonString}'");
			throw;
		}
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
