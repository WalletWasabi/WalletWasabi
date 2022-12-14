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

namespace WalletWasabi.Affiliation;

public class AffiliateServerHttpApiClient
{
	private IHttpClient Client;

	public AffiliateServerHttpApiClient(IHttpClient client)
	{
		Client = client;
	}

	private enum RemoteAction
	{
		GetCoinjoinRequest,
		GetStatus
	}

	public Task<GetCoinjoinRequestResponse> GetCoinjoinRequest(GetCoinjoinRequestRequest request, CancellationToken cancellationToken) =>
		SendAsync<GetCoinjoinRequestRequest, GetCoinjoinRequestResponse>(RemoteAction.GetCoinjoinRequest, request, TimeSpan.FromSeconds(10), 6, cancellationToken);

	public Task<StatusResponse> GetStatus(StatusRequest request, CancellationToken cancellationToken) =>
		SendAsync<StatusRequest, StatusResponse>(RemoteAction.GetStatus, request, TimeSpan.FromSeconds(10), 2, cancellationToken);

	private async Task<HttpResponseMessage> SendAsync(RemoteAction action, string jsonString, TimeSpan requestTimeout, CancellationToken cancellationToken)
	{
		using StringContent content = new(jsonString, Encoding.UTF8, "application/json");

		using CancellationTokenSource requestTimeoutCTS = new(requestTimeout);
		using CancellationTokenSource linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, requestTimeoutCTS.Token);

		return await Client.SendAsync(HttpMethod.Post, GetUriEndPoint(action), content, linkedCTS.Token);
	}

	private async Task<HttpResponseMessage> SendAsync(RemoteAction action, string jsonString, TimeSpan requestTimeout, int requestNumber, CancellationToken cancellationToken)
	{
		Exception GetFinalException(IEnumerable<Exception> exceptions) =>
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
				return await SendAsync(action, jsonString, requestTimeout, cancellationToken);
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
		string requestSerialized = Serialize(request);
		using HttpResponseMessage response = await SendAsync(action, Serialize(request), requestTimeout, requestNumber, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowUnwrapExceptionFromContentAsync(cancellationToken);
		}

		string responseSerialized = await response.Content.ReadAsStringAsync(cancellationToken);
		return Deserialize<TResponse>(responseSerialized);
	}


	private static string Serialize<T>(T obj)
	{
		try
		{
			return JsonConvert.SerializeObject(obj, JsonSerializationOptions.Settings);
		}
		catch
		{
			throw new Exception("Serialization error.");
		}
	}

	private static TResponse Deserialize<TResponse>(string jsonString)
	{
		try
		{
			return JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Settings)
				?? throw new Exception();
		}
		catch
		{
			throw new Exception("Deserialization error.");
		}
	}

	private static string GetUriEndPoint(RemoteAction action) =>
		action switch
		{
			RemoteAction.GetCoinjoinRequest => "get_coinjoin_request",
			RemoteAction.GetStatus => "get_status",
			_ => throw new NotSupportedException($"Action '{action}' is not supported.")
		};
}
