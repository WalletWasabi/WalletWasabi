using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Logging;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.Serialization;

namespace WalletWasabi.WabiSabi.Client;

public class WabiSabiHttpApiClient : IWabiSabiApiRequestHandler
{
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
		var exceptions = new Dictionary<Exception, int>();
		var start = DateTime.UtcNow;

		using CancellationTokenSource absoluteTimeoutCts = new(TimeSpan.FromMinutes(30));
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, absoluteTimeoutCts.Token);
		var combinedToken = linkedCts.Token;

		var attempt = 1;
		do
		{
			try
			{
				using StringContent content = new(jsonString, Encoding.UTF8, "application/json");

				// Any transport layer errors will throw an exception here.
				HttpResponseMessage response = await _client
					.SendAsync(HttpMethod.Post, GetUriEndPoint(action), content, combinedToken).ConfigureAwait(false);

				TimeSpan totalTime = DateTime.UtcNow - start;

				if (exceptions.Any())
				{
					Logger.LogDebug(
						$"Received a response for {action} in {totalTime.TotalSeconds:0.##s} after {attempt} failed attempts: {new AggregateException(exceptions.Keys)}.");
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
				Logger.LogTrace($"Attempt {attempt} failed with {nameof(HttpRequestException)}: {e.Message}.");
				AddException(exceptions, e);
			}
			catch (TorException e)
			{
				Logger.LogTrace($"Attempt {attempt} failed with {nameof(TorException)}: {e.Message}.");
				AddException(exceptions, e);
			}
			catch (OperationCanceledException e)
			{
				Logger.LogTrace($"Attempt {attempt} failed with {nameof(OperationCanceledException)}: {e.Message}.");
				AddException(exceptions, e);
			}
			catch (Exception e)
			{
				Logger.LogDebug($"Attempt {attempt} failed with exception {e}.");

				if (exceptions.Any())
				{
					AddException(exceptions, e);
					throw new AggregateException(exceptions.Keys);
				}
				else
				{
					throw;
				}
			}

			try
			{
				// Wait before the next try.
				await Task.Delay(250, combinedToken).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				AddException(exceptions, e);
			}

			attempt++;
		}
		while (!combinedToken.IsCancellationRequested);

		throw new AggregateException(exceptions.Keys);
	}

	private static void AddException(Dictionary<Exception, int> exceptions, Exception e)
	{
		bool Predicate(KeyValuePair<Exception, int> x) => e.GetType() == x.Key.GetType() && e.Message == x.Key.Message;

		if (exceptions.Any(Predicate))
		{
			var first = exceptions.First(Predicate);
			exceptions[first.Key]++;
		}
		else
		{
			exceptions.Add(e, 1);
		}
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
