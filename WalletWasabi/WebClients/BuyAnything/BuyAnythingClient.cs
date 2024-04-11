using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Country = WalletWasabi.BuyAnything.Country;

namespace WalletWasabi.WebClients.BuyAnything;

public class BuyAnythingClient
{
	// Product Id mapping for Concierge services
	private static readonly Dictionary<Product, string> ProductIdsProduction = new()
	{
		[Product.ConciergeRequest] = "018c0cec5299719f9458dba04f88eb8c",
		[Product.FastTravelBooking] = "018c0cef890970ea9b143994f9930331",
		[Product.TravelConcierge] = "018c0cf0e5fc70bc9255b0cdb4510dbd"
	};

	// Product Id mapping for Concierge services
	private static readonly Dictionary<Product, string> ProductIdsTesting = new()
	{
		[Product.ConciergeRequest] = "018d313972cf7c45b5fe2af5bce6e55d",
		[Product.FastTravelBooking] = "018d313a605e7beb9ea605542267d8f8",
		[Product.TravelConcierge] = "018d313bc5c4744281ec5ed837cee1c5"
	};

	private static readonly string SalutationIdProduction = "018b6635785b70679f479eadf50330f3";
	private static readonly string SalutationIdTesting = "018d18f29d347170b6cfd6466cab3c71";

	private static readonly string StorefrontUrlProduction = "https://wasabi.shopinbit.com";
	private static readonly string StorefrontUrlTesting = "https://shopinbit.solution360.dev/wasabi";

	// Customer information. We need this values to update the messages
	// we have three options:
	// 1. Create a new customer with random names and store them in the disk
	// 2. Use {firstName}.{lastName}@me.com as the email address and store that (it makes sense if need to log in customers)
	// 3. Hardcode the values here
	private static readonly string FirstName = "Watoshi";

	private static readonly string LastName = "Sabimoto";

	public BuyAnythingClient(IShopWareApiClient apiClient, bool useTestApi = false)
	{
		ApiClient = apiClient;
		ProductIds = useTestApi ? ProductIdsTesting : ProductIdsProduction;
		SalutationId = useTestApi ? SalutationIdTesting : SalutationIdProduction;
		StorefrontUrl = useTestApi ? StorefrontUrlTesting : StorefrontUrlProduction;
	}

	// Services provided by Concierge
	public enum Product
	{
		[Description("All-Purpose Concierge Assistant")]
		ConciergeRequest,

		[Description("Fast Travel Assistant")]
		FastTravelBooking,

		[Description("General Travel Assistant")]
		TravelConcierge
	}

	// Product Id mapping for Concierge services
	private Dictionary<Product, string> ProductIds { get; }

	private string SalutationId { get; }
	private string StorefrontUrl { get; }

	private IShopWareApiClient ApiClient { get; }
	private AsyncLock ContextTokenCacheLock { get; } = new();

	private Dictionary<string, (string, DateTime)> ContextTokenCache { get; } = new();

	// Creates a new "conversation" (or Request). This means that we have to:
	// 1. Create a dummy customer
	// 2. Create a shopping cart for the customer
	// 3. Add an item to the shopping cart (The service to request)
	// 4. Generate an order by checking out the shopping cart and adding a customer comment to it.
	public async Task<(string OrderId, string OrderNumber)> CreateNewConversationAsync(string emailAddress, string password, string countryId, Product product, string comment, CancellationToken cancellationToken)
	{
		// Messages to use
		var customerRegistrationRequest = ShopWareRequestFactory.CustomerRegistrationRequest(
			SalutationId, FirstName, LastName, emailAddress, password, countryId, comment, StorefrontUrl);
		var shoppingCartCreationRequest = ShopWareRequestFactory.ShoppingCartCreationRequest("My shopping cart");
		var shoppingCartItemAdditionRequest = ShopWareRequestFactory.ShoppingCartItemsRequest(ProductIds[product]);
		var orderGenerationRequest = ShopWareRequestFactory.OrderGenerationRequest();

		// Create the conversation
		var customerRegistrationResponse = await ApiClient.RegisterCustomerAsync("new-context", customerRegistrationRequest, cancellationToken).ConfigureAwait(false);

		// Get the context token (session identifier) for the created user. In same cases, as customer registration,
		// we can get two context tokens. The first one is for the recently created user and the second one is for the
		// user that created the new new user.
		var ctxToken = customerRegistrationResponse.ContextTokens[0];

		// Note: When we create a shopping cart, we receive a new context token but it is identical to the one that was
		// used to create it so, I don't know whether it makes any sense to use it or not. Here we use the same context
		// token.

		var shoppingCartCreationResponse = await ApiClient.GetOrCreateShoppingCartAsync(ctxToken, shoppingCartCreationRequest, cancellationToken).ConfigureAwait(false);
		var shoppingCartItemAdditionResponse = await ApiClient.AddItemToShoppingCartAsync(ctxToken, shoppingCartItemAdditionRequest, cancellationToken).ConfigureAwait(false);
		var orderGenerationResponse = await ApiClient.GenerateOrderAsync(ctxToken, orderGenerationRequest, cancellationToken).ConfigureAwait(false);

		return (orderGenerationResponse.Id, orderGenerationResponse.OrderNumber);
	}

	public async Task UpdateConversationAsync(NetworkCredential credential, string rawText, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		await ApiClient.UpdateCustomerProfileAsync(ctxToken, ShopWareRequestFactory.CustomerProfileUpdateRequest(FirstName, LastName, rawText), cancellationToken).ConfigureAwait(false);
	}

	public async Task SetBillingAddressAsync(NetworkCredential credential, string firstName, string lastName, string address, string houseNumber, string zipCode, string city, string stateId, string countryId, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var request = ShopWareRequestFactory.BillingAddressRequest(firstName, lastName, address, houseNumber, zipCode, city, stateId, countryId);
		await ApiClient.UpdateCustomerBillingAddressAsync(ctxToken, request, cancellationToken).ConfigureAwait(false);
	}

	public async Task<Order[]> GetOrdersUpdateAsync(NetworkCredential credential, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var request = ShopWareRequestFactory.GetOrderListRequest();
		var orderList = await ApiClient.GetOrderListAsync(ctxToken, request, cancellationToken).ConfigureAwait(false);
		return orderList.Orders.Elements;
	}

	public async Task<CustomerProfileResponse> GetCustomerProfileAsync(NetworkCredential credential, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var customerProfileResponse = await ApiClient.GetCustomerProfileAsync(ctxToken, cancellationToken).ConfigureAwait(false);
		return customerProfileResponse;
	}

	public async Task HandlePaymentAsync(NetworkCredential credential, string orderId, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var request = ShopWareRequestFactory.PaymentRequest(orderId);
		await ApiClient.HandlePaymentAsync(ctxToken, request, cancellationToken).ConfigureAwait(false);
	}

	public async Task<Country[]> GetCountriesAsync(CancellationToken cancellationToken)
	{
		var results = new List<Country>();
		var currentPage = 0;
		while (true)
		{
			currentPage++;

			var countryResponse = await ApiClient.GetCountriesAsync("none", ShopWareRequestFactory.GetPage(currentPage, 100), cancellationToken).ConfigureAwait(false);
			var cachedCountries = countryResponse.Elements
				.Where(x => x.Active)
				.Select(x => new Country(
					Id: x.Id,
					Name: x.Name));

			results.AddRange(cachedCountries);

			if (countryResponse.Total != countryResponse.Limit)
			{
				break;
			}
			cancellationToken.ThrowIfCancellationRequested();
		}
		return results.ToArray();
	}

	public async Task<State[]> GetStatesByCountryIdAsync(string countryId, CancellationToken cancellationToken)
	{
		var stateResponse = await ApiClient.GetStatesByCountryIdAsync("", countryId, cancellationToken).ConfigureAwait(false);
		return stateResponse.Elements.ToArray();
	}

	// Login the customer and return the context token.
	// This method implements a caching mechanism to avoid multiple login requests.
	private async Task<string> LoginAsync(NetworkCredential credential, CancellationToken cancellationToken)
	{
		using (await ContextTokenCacheLock.LockAsync(cancellationToken).ConfigureAwait(false))
		{
			if (ContextTokenCache.TryGetValue(credential.UserName, out (string token, DateTime expriresAt) cacheEntry))
			{
				if (cacheEntry.expriresAt > DateTimeOffset.UtcNow)
				{
					return cacheEntry.token;
				}
			}
			var request = ShopWareRequestFactory.CustomerLoginRequest(credential.UserName, credential.Password);
			var response = await ApiClient.LoginCustomerAsync("new-context", request, cancellationToken).ConfigureAwait(false);
			ContextTokenCache[credential.UserName] = (response.ContextToken, DateTime.UtcNow.AddMinutes(10));
			return response.ContextToken;
		}
	}
}
