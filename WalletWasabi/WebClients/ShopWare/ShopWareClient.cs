using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.ShopWare;

public class ShopWareApiClient
{
	private readonly HttpClient httpClient;
	private readonly string apiUrl;
	private readonly string apiKey;

	public ShopWareApiClient(HttpClient client, string apiKey)
	{
		this.apiUrl = apiUrl;
		this.apiKey = apiKey;
		_client = client;
		// Initialize HttpClient with required headers
		_client.DefaultRequestHeaders.Add("Accept", "application/json");
		_client.DefaultRequestHeaders.Add("Content-Type", "application/json");
		_client.DefaultRequestHeaders.Add("sw-access-key", apiKey);
	}

	private HttpClient _client;

	private enum RemoteAction
	{
		RegisterCustomer
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(CustomerRegistrationRequest request,
		CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationRequest, CustomerRegistrationResponse>(RemoteAction.RegisterCustomer, request, cancellationToken);

	private async Task<string> SendAsync<TRequest>(RemoteAction action, TRequest request,
		CancellationToken cancellationToken) where TRequest : class
	{
		using var content = Serialize(request);
		using var httpRequest = new HttpRequestMessage(HttpMethod.Post, GetUriEndPoint(action));
		httpRequest.Content = content;
		using var response = await _client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		var ctxTokens = response.Headers.GetValues("sw-context-token").ToArray();
		if (JsonConvert.DeserializeObject(responseBody) is JObject jObject)
		{
			jObject.Add("ContextTokens", JArray.FromObject(ctxTokens));
			return JsonConvert.SerializeObject(jObject);
		}

		return responseBody;
	}

	private async Task SendAndReceiveAsync<TRequest>(RemoteAction action, TRequest request,
		CancellationToken cancellationToken) where TRequest : class
	{
		await SendAsync(action, request, cancellationToken).ConfigureAwait(false);
	}

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(RemoteAction action, TRequest request,
		CancellationToken cancellationToken) where TRequest : class
	{
		var jsonString = await SendAsync(action, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private static StringContent Serialize<T>(T obj)
	{
		string jsonString = JsonConvert.SerializeObject(obj, JsonSerializationOptions.Default.Settings);
		return new StringContent(jsonString, Encoding.UTF8, "application/json");
	}

	private static TResponse Deserialize<TResponse>(string jsonString)
	{
		return JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings)
		       ?? throw new InvalidOperationException("Deserialization error");
	}


	private static string GetUriEndPoint(RemoteAction action) =>
		"store-api/" + action switch
		{
			RemoteAction.RegisterCustomer => "account/register",
			_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
		};
}
