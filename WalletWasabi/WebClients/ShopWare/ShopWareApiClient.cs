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
using GetOrderListRequest = WalletWasabi.WebClients.ShopWare.Models.Unit;
using CancelOrderResponse = WalletWasabi.WebClients.ShopWare.Models.StateMachineState;

namespace WalletWasabi.WebClients.ShopWare;

public class ShopWareApiClient
{
	private HttpClient _client;

	public ShopWareApiClient(HttpClient client, string apiKey)
	{
		_client = client;

		// Initialize HttpClient with required headers
		_client.DefaultRequestHeaders.Add("Accept", "application/json");
		_client.DefaultRequestHeaders.Add("sw-access-key", apiKey);
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, CustomerRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationRequest, CustomerRegistrationResponse>(ctxToken, HttpMethod.Post, "account/register" , request, cancellationToken);

	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, ShoppingCartCreationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartCreationRequest, ShoppingCartResponse>(ctxToken, HttpMethod.Post, "checkout/cart", request, cancellationToken);

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, ShoppingCartItemsRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartItemsRequest, ShoppingCartItemsResponse>(ctxToken, HttpMethod.Post, "checkout/cart/line-item", request, cancellationToken);

	public Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, OrderGenerationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OrderGenerationRequest, OrderGenerationResponse>(ctxToken, HttpMethod.Post, "checkout/order", request, cancellationToken);

	public Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<Unit, GetOrderListResponse>(ctxToken, HttpMethod.Post, "order", Unit.Instance, cancellationToken);

	public Task<CancelOrderResponse> CancelOrderAsync(string ctxToken, CancelOrderRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CancelOrderRequest, CancelOrderResponse>(ctxToken, HttpMethod.Post, "order/state/cancel", request, cancellationToken);

	public Task UpdateOrderAsync(string ctxToken, UpdateOrderRequest request, CancellationToken cancellationToken) =>
		SendAsync(ctxToken, HttpMethod.Patch, $"order/{request.OrderId}", request, cancellationToken);

	public Task<GetCountryResponse> GetCountryByNameAsync(string ctxToken, GetCountryRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetCountryRequest, GetCountryResponse>(ctxToken, HttpMethod.Post, "country", request, cancellationToken);

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(string ctxToken, HttpMethod httpMethod, string path, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
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
