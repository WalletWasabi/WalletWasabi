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

	private enum RemoteAction
	{
		RegisterCustomer,
		GetOrCreateShoppingCart,
		AddItemToShoppingCart,
		GenerateOrder,
		GetCountry,
		GetOrderList,
		CancelOrder
	}

	public ShopWareApiClient(HttpClient client, string apiKey)
	{
		_client = client;

		// Initialize HttpClient with required headers
		_client.DefaultRequestHeaders.Add("Accept", "application/json");
		_client.DefaultRequestHeaders.Add("sw-access-key", apiKey);
	}

	public Task<CustomerRegistrationResponse> RegisterCustomerAsync(string ctxToken, CustomerRegistrationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CustomerRegistrationRequest, CustomerRegistrationResponse>(ctxToken, RemoteAction.RegisterCustomer, request, cancellationToken);

	public Task<ShoppingCartResponse> GetOrCreateShoppingCartAsync(string ctxToken, ShoppingCartCreationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartCreationRequest, ShoppingCartResponse>(ctxToken, RemoteAction.GetOrCreateShoppingCart, request, cancellationToken);

	public Task<ShoppingCartItemsResponse> AddItemToShoppingCartAsync(string ctxToken, ShoppingCartItemsRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<ShoppingCartItemsRequest, ShoppingCartItemsResponse>(ctxToken, RemoteAction.AddItemToShoppingCart, request, cancellationToken);

	public Task<OrderGenerationResponse> GenerateOrderAsync(string ctxToken, OrderGenerationRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<OrderGenerationRequest, OrderGenerationResponse>(ctxToken, RemoteAction.GenerateOrder, request, cancellationToken);

	public Task<GetOrderListResponse> GetOrderListAsync(string ctxToken, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<Unit, GetOrderListResponse>(ctxToken, RemoteAction.GetOrderList, Unit.Instance, cancellationToken);

	public Task<CancelOrderResponse> CancelOrderAsync(string ctxToken, CancelOrderRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<CancelOrderRequest, CancelOrderResponse>(ctxToken, RemoteAction.CancelOrder, request, cancellationToken);

	public Task<GetCountryResponse> GetCountryByNameAsync(string ctxToken, GetCountryRequest request, CancellationToken cancellationToken) =>
		SendAndReceiveAsync<GetCountryRequest, GetCountryResponse>(ctxToken, RemoteAction.GetCountry, request, cancellationToken);

	private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(string ctxToken, RemoteAction action, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
	{
		var jsonString = await SendAsync(ctxToken, action, request, cancellationToken).ConfigureAwait(false);
		return Deserialize<TResponse>(jsonString);
	}

	private async Task<string> SendAsync<TRequest>(string ctxToken, RemoteAction action, TRequest request, CancellationToken cancellationToken)
		where TRequest : class
	{
		var (httpMethod, path) = GetUriEndPoint(action);
		using var httpRequest = new HttpRequestMessage(httpMethod, path);
		httpRequest.Headers.Add("sw-context-token", ctxToken);
		using var content = Serialize(request);
		if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
		{
			httpRequest.Content = content;
		}

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

	private static (HttpMethod, string) GetUriEndPoint(RemoteAction action) =>
		action switch
		{
			RemoteAction.RegisterCustomer => (HttpMethod.Post, "account/register"),
			RemoteAction.GetOrCreateShoppingCart => (HttpMethod.Post, "checkout/cart"),
			RemoteAction.AddItemToShoppingCart => (HttpMethod.Post, "checkout/cart/line-item"),
			RemoteAction.GenerateOrder => (HttpMethod.Post, "checkout/order"),
			RemoteAction.GetCountry => (HttpMethod.Post, "country"),
			RemoteAction.GetOrderList => (HttpMethod.Post, "order"),
			RemoteAction.CancelOrder => (HttpMethod.Post, "order/state/cancel"),
			_ => throw new NotSupportedException($"Action '{action}' is unknown and has no endpoint associated.")
		};
}
