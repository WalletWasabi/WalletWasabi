using WalletWasabi.Affiliation.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Affiliation.Serialization;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Affiliation;

public class AffiliateServerHttpApiClient
{
	public AffiliateServerHttpApiClient(IHttpClient client)
	{
		Client = client;
	}

	private IHttpClient Client { get; }

	private enum RemoteAction
	{
		NotifyCoinJoin,
		GetStatus
	}

	public Task<CoinJoinNotificationResponse> NotifyCoinJoinAsync(CoinJoinNotificationRequest request, CancellationToken cancellationToken) =>
		SendAsync<CoinJoinNotificationRequest, CoinJoinNotificationResponse>(RemoteAction.NotifyCoinJoin, request, TimeSpan.FromSeconds(10), 6, cancellationToken);

	public Task<Unit> GetStatusAsync(CancellationToken cancellationToken) =>
		SendAsync<Unit, Unit>(RemoteAction.GetStatus, Unit.Instance, TimeSpan.FromSeconds(10), 2, cancellationToken);

	private async Task<HttpResponseMessage> SendAsync(RemoteAction action, string jsonString, TimeSpan requestTimeout, CancellationToken cancellationToken)
	{
		using StringContent content = new(jsonString, Encoding.UTF8, "application/json");

		using var linkedCts = cancellationToken.CreateLinkedTokenSourceWithTimeout(requestTimeout);

		return await Client.SendAsync(HttpMethod.Post, GetUriEndPoint(action), content, linkedCts.Token).ConfigureAwait(false);
	}

	private async Task<HttpResponseMessage> SendAsync(RemoteAction action, string jsonString, TimeSpan requestTimeout, int requestNumber, CancellationToken cancellationToken)
	{
		static Exception GetFinalException(IEnumerable<Exception> exceptions) =>
			(exceptions.Count()) switch
			{
				0 => throw new InvalidOperationException(),
				1 => throw exceptions.First(),
				_ => throw new AggregateException(exceptions)
			};

		List<Exception> exceptions = new();

		foreach (var _ in Enumerable.Range(0, requestNumber))
		{
			try
			{
				return await SendAsync(action, jsonString, requestTimeout, cancellationToken).ConfigureAwait(false);
			}
			catch (HttpRequestException httpRequestException)
			{
				exceptions.Add(httpRequestException);
			}
			catch (OperationCanceledException operationCanceledException)
			{
				exceptions.Add(operationCanceledException);
			}
			catch (Exception exception)
			{
				exceptions.Add(exception);
				throw GetFinalException(exceptions);
			}

			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}

		throw GetFinalException(exceptions);
	}

	private async Task<TResponse> SendAsync<TRequest, TResponse>(RemoteAction action, TRequest request, TimeSpan requestTimeout, int requestNumber, CancellationToken cancellationToken)
		  where TRequest : class
		  where TResponse : class
	{
		using HttpResponseMessage response = await SendAsync(action, Serialize(request), requestTimeout, requestNumber, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowUnwrapExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		string responseSerialized = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(responseSerialized);
	}

	private static string Serialize<T>(T obj) =>
		JsonConvert.SerializeObject(obj, AffiliationJsonSerializationOptions.Settings);

	private static TResponse Deserialize<TResponse>(string jsonString)
	{
		try
		{
			return JsonConvert.DeserializeObject<TResponse>(jsonString, AffiliationJsonSerializationOptions.Settings)
				?? throw new InvalidOperationException("Deserialization error");
		}
		catch
		{
			Logger.LogDebug($"Failed to deserialize {typeof(TResponse)} from JSON '{jsonString}'");
			throw;
		}
	}

	private static string GetUriEndPoint(RemoteAction action) =>
		action switch
		{
			RemoteAction.NotifyCoinJoin => "notify_coinjoin",
			RemoteAction.GetStatus => "get_status",
			_ => throw new NotSupportedException($"Action '{action}' is not supported.")
		};
}
