using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients.ShopWare.Models;
using CancelOrderResponse = WalletWasabi.WebClients.ShopWare.Models.StateMachineState;
using CustomerProfileUpdateResponse = WalletWasabi.WebClients.ShopWare.Models.PropertyBag;

namespace WalletWasabi.WebClients.ShopWare;

public class ShopWareApiClient
{
	private HttpClient _client;

	// Initializes a new instance of the ShopWareApiClient class.
	//
	// Parameters:
	//   client:
	//     The HttpClient to use for making API requests.
	//
	//   apiKey:
	//     The API key to authenticate the requests.
	public ShopWareApiClient(HttpClient client, string apiKey)
	{
		_client = client;

		// Initialize HttpClient with required headers
		_client.DefaultRequestHeaders.Add("Accept", "application/json");
		_client.DefaultRequestHeaders.Add("sw-access-key", apiKey); // API key. Must be in every single request.
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationResponse>(ctxToken, HttpMethod.Post, "account/register", request, cancellationToken);

	public Task<CustomerLoginResponse> LoginCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerLoginResponse>(ctxToken, HttpMethod.Post, "account/login", request, cancellationToken);

	public Task<CustomerProfileUpdateResponse> UpdateCustomerProfileAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerProfileUpdateResponse>(ctxToken, HttpMethod.Post, "account/change-profile", request, cancellationToken);
	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartResponse>(ctxToken, HttpMethod.Post, "checkout/cart", request, cancellationToken);

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartItemsResponse>(ctxToken, HttpMethod.Post, "checkout/cart/line-item", request, cancellationToken);

	public Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OrderGenerationResponse>(ctxToken, HttpMethod.Post, "checkout/order", request, cancellationToken);

	public Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetOrderListResponse>(ctxToken, HttpMethod.Post, "order", PropertyBag.Empty, cancellationToken);

	public Task<CancelOrderResponse> CancelOrderAsync(string ctxToken,  PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CancelOrderResponse>(ctxToken, HttpMethod.Post, "order/state/cancel", request, cancellationToken);

	public Task<GetCountryResponse> GetCountriesAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetCountryResponse>(ctxToken, HttpMethod.Post, "country", request, cancellationToken);

	private async Task<TResponse> SendAndReceiveAsync<TResponse>(string ctxToken, HttpMethod httpMethod, string path, PropertyBag request, CancellationToken cancellationToken)
	{
		var jsonString = await SendAsync(ctxToken, httpMethod, path, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private async Task<string> SendAsync<TRequest>(string ctxToken, HttpMethod httpMethod, string path, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
	{
		using var httpRequest = new HttpRequestMessage(httpMethod, path);
		httpRequest.Headers.Add("sw-context-token", ctxToken);
		using var content = Serialize(request);
		if (httpMethod != HttpMethod.Get)
		{
			httpRequest.Content = content;
		}
#if false
		// This is only for testing. And sends the request to the localhost so we can see exacty what is being sent.
		// to the network without needing to spy on it.
		// In a terminal use: nc -l 9090
		if (path.StartsWith("order/"))
		{
			var client = new HttpClient();
			client.BaseAddress = new Uri("http://127.0.0.1:9090/store-api/");
			client.DefaultRequestHeaders.Add("sw-access-key", _client.DefaultRequestHeaders.GetValues("sw-access-key"));
			_client = client;
		}
#endif
		using var response = await _client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessStatusCode)
		{
			await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
		}

		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

		// Here we read the context tokens from the response headers.
		// and add them to the request json body such that they can be deserialized transparently for those
		// types that include a `string[] ContextTokens` property.
		var ctxTokens = response.Headers.GetValues("sw-context-token").ToArray();
		if (JsonConvert.DeserializeObject(responseBody) is JObject jObject)
		{
			jObject.Add("ContextTokens", JArray.FromObject(ctxTokens));
			return JsonConvert.SerializeObject(jObject);
		}

		return responseBody;
	}

	private static StringContent Serialize<T>(T obj)
	{
		string jsonString = JsonConvert.SerializeObject(obj,
			new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			});
		return new StringContent(jsonString, Encoding.UTF8, "application/json");
	}

	private static TResponse Deserialize<TResponse>(string jsonString)
	{
		return JsonConvert.DeserializeObject<TResponse>(jsonString, JsonSerializationOptions.Default.Settings)
			   ?? throw new InvalidOperationException("Deserialization error");
	}
}
