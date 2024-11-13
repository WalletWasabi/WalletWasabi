using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WalletWasabi.Extensions;
using WalletWasabi.Tor.Http;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients.ShopWare.Models;
using CancelOrderResponse = WalletWasabi.WebClients.ShopWare.Models.StateMachineState;
using CustomerProfileUpdateResponse = WalletWasabi.WebClients.ShopWare.Models.PropertyBag;

using BillingAddressResponse = WalletWasabi.WebClients.ShopWare.Models.PropertyBag;

namespace WalletWasabi.WebClients.ShopWare;

public class ShopWareApiClient : IShopWareApiClient
{
	private HttpClient _client;
	private string _apiKey;

	// Initializes a new instance of the ShopWareApiClient class.
	//
	// Parameters:
	//   client:
	//     The HttpClient to use for making API requests.
	//
	//   apiKey:
	//     The API key to authenticate the requests.
	public ShopWareApiClient(HttpClient client, Uri apiUri, string apiKey)
	{
		_client = client;
		_client.BaseAddress = apiUri;
		_apiKey = apiKey;
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationResponse>(ctxToken, HttpMethod.Post, "account/register", request, cancellationToken);

	public Task<CustomerLoginResponse> LoginCustomerAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerLoginResponse>(ctxToken, HttpMethod.Post, "account/login", request, cancellationToken);

	public Task<CustomerProfileUpdateResponse> UpdateCustomerProfileAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerProfileUpdateResponse>(ctxToken, HttpMethod.Post, "account/change-profile", request, cancellationToken);

	public Task<BillingAddressResponse> UpdateCustomerBillingAddressAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<BillingAddressResponse>(ctxToken, HttpMethod.Post, "account/address", request, cancellationToken);

	public Task<CustomerProfileResponse> GetCustomerProfileAsync(string ctxToken, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerProfileResponse>(ctxToken, HttpMethod.Post, "account/customer", PropertyBag.Empty, cancellationToken);

	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartResponse>(ctxToken, HttpMethod.Post, "checkout/cart", request, cancellationToken);

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartItemsResponse>(ctxToken, HttpMethod.Post, "checkout/cart/line-item", request, cancellationToken);

	public Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OrderGenerationResponse>(ctxToken, HttpMethod.Post, "checkout/order", request, cancellationToken);

	public Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetOrderListResponse>(ctxToken, HttpMethod.Post, "order", request, cancellationToken);

	public Task<CancelOrderResponse> CancelOrderAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CancelOrderResponse>(ctxToken, HttpMethod.Post, "order/state/cancel", request, cancellationToken);

	public Task<GetCountryResponse> GetCountriesAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetCountryResponse>(ctxToken, HttpMethod.Post, "country", request, cancellationToken);

	public Task<GetStateResponse> GetStatesByCountryIdAsync(string ctxToken, string countryId, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetStateResponse>(ctxToken, HttpMethod.Get, $"country-state/{countryId}", PropertyBag.Empty, cancellationToken);

	public Task<HandlePaymentResponse> HandlePaymentAsync(string ctxToken, PropertyBag request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<HandlePaymentResponse>(ctxToken, HttpMethod.Post, "handle-payment", request, cancellationToken);

	private async Task<TResponse> SendAndReceiveAsync<TResponse>(string ctxToken, HttpMethod httpMethod, string path, PropertyBag request, CancellationToken cancellationToken)
	{
		var jsonString = await SendAsync(ctxToken, httpMethod, path, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private async Task<string> SendAsync<TRequest>(string ctxToken, HttpMethod httpMethod, string path, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
	{
		try
		{
			using var httpRequest = new HttpRequestMessage(httpMethod, path);

			httpRequest.Headers.Add("sw-context-token", ctxToken);
			httpRequest.Headers.Add("Accept", "application/json");
			httpRequest.Headers.Add("sw-access-key", _apiKey);

			using var content = Serialize(request);
			if (httpMethod != HttpMethod.Get)
			{
				httpRequest.Content = content;
			}

			WriteRequest(httpRequest, request);
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
		catch (Exception)
		{
			throw;
		}
	}

	[Conditional("DEBUG")]
	private void WriteRequest<TRequest>(HttpRequestMessage httpRequest, TRequest request)
	{
		string body = JsonConvert.SerializeObject(
			request,
			new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			});
		Debug.WriteLine($@"""
			curl --request {httpRequest.Method.Method} \
                 --url {httpRequest.RequestUri} \
                 --header 'Accept: application/json' \
                 --header 'Content-Type: application/json' \
                 --header 'sw-access-key: {string.Join(",", httpRequest.Headers.GetValues("sw-access-key"))}' \
                 --header 'sw-context-token: {string.Join(",", httpRequest.Headers.GetValues("sw-context-token"))}' \
                 --data '{body}'
			""");
	}

	private static StringContent Serialize<T>(T obj)
	{
		string jsonString = JsonConvert.SerializeObject(
			obj,
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
