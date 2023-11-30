using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WalletWasabi.Tor.Http;
using WalletWasabi.Tor.Http.Extensions;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients.ShopWare.Models;
using WalletWasabi.WebClients.Wasabi;
using CancelOrderResponse = WalletWasabi.WebClients.ShopWare.Models.StateMachineState;
using CustomerProfileUpdateResponse = WalletWasabi.WebClients.ShopWare.Models.PropertyBag;
using BillingAddressResponse = WalletWasabi.WebClients.ShopWare.Models.PropertyBag;

namespace WalletWasabi.WebClients.ShopWare;

public class ShopWareApiClient
{
	private static readonly Uri BaseUri = new("https://shopinbit.com/store-api/");

	private readonly IHttpClient _client;
	private readonly string _apiKey;

	// Initializes a new instance of the ShopWareApiClient class.
	//
	// Parameters:
	//   client:
	//     The HttpClient to use for making API requests.
	//
	//   apiKey:
	//     The API key to authenticate the requests.
	public ShopWareApiClient(HttpClientFactory httpClientFactory, string apiKey)
	{
		_apiKey = apiKey;
		_client = httpClientFactory.NewHttpClient(() => BaseUri, Mode.SingleCircuitPerLifetime);
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationResponse>(null, HttpMethod.Post, "account/register", request, cancellationToken);

	public Task<CustomerLoginResponse> LoginCustomerAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerLoginResponse>(customer, HttpMethod.Post, "account/login", request, cancellationToken);

	public Task<CustomerProfileUpdateResponse> UpdateCustomerProfileAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerProfileUpdateResponse>(customer, HttpMethod.Post, "account/change-profile", request, cancellationToken);

	public Task<BillingAddressResponse> UpdateCustomerBillingAddressAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<BillingAddressResponse>(customer, HttpMethod.Post, "account/address", request, cancellationToken);

	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartResponse>(customer, HttpMethod.Post, "checkout/cart", request, cancellationToken);

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartItemsResponse>(customer, HttpMethod.Post, "checkout/cart/line-item", request, cancellationToken);

	public Task<OrderGenerationResponse> GenerateOrderAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OrderGenerationResponse>(customer, HttpMethod.Post, "checkout/order", request, cancellationToken);

	public Task<GetOrderListResponse> GetOrderListAsync(LocalCustomer? customer, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetOrderListResponse>(customer, HttpMethod.Post, "order", PropertyBag.Empty, cancellationToken);

	public Task<CancelOrderResponse> CancelOrderAsync(LocalCustomer? customer, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CancelOrderResponse>(customer, HttpMethod.Post, "order/state/cancel", request, cancellationToken);

	public Task<GetCountryResponse> GetCountriesAsync(PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetCountryResponse>(null, HttpMethod.Post, "country", request, cancellationToken);


	private async Task<TResponse> SendAndReceiveAsync<TResponse>(LocalCustomer? customer, HttpMethod httpMethod, string path, PropertyBag request, CancellationToken cancellationToken)
	{
		var jsonString = await SendAsync(customer, httpMethod, path, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private async Task<string> SendAsync<TRequest>(LocalCustomer? customer, HttpMethod? httpMethod, string path, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
	{
		using var httpRequest = new HttpRequestMessage(httpMethod, path);

		httpRequest.Headers.Add("Accept", "application/json");
		httpRequest.Headers.Add("sw-access-key", _apiKey);
		httpRequest.Headers.Add("sw-context-token", customer is null ? "none" : customer.LastKnownAccessToken);

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
			// Todo: /!\ If login failed, try again with LoginCustomerAsync /!\ Verify, possibly move code to client
			if (response.StatusCode == HttpStatusCode.Unauthorized && customer != null)
			{
				// If we can't login it will throw which is what we want
				var result = await LoginCustomerAsync(customer, ShopWareRequestFactory.CustomerLoginRequest(customer.Email, customer.Password),
					cancellationToken).ConfigureAwait(false);

				// Save the new context token
				customer.LastKnownAccessToken = result.ContextToken;
			}
			else
			{
				await response.ThrowRequestExceptionFromContentAsync(cancellationToken).ConfigureAwait(false);
			}
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
