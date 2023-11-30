using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.BuyAnything;

public class BuyAnythingClient
{
	// Services provided by Concierge
	public enum Product
	{
		ConciergeRequest,
		FastTravelBooking,
		TravelConcierge
	}

	// Product Id mapping for Concierge services
	private static readonly Dictionary<Product, string> ProductIds = new()
	{
		[Product.ConciergeRequest] = "018c0cec5299719f9458dba04f88eb8c",
		[Product.FastTravelBooking] = "018c0cef890970ea9b143994f9930331",
		[Product.TravelConcierge] = "018c0cf0e5fc70bc9255b0cdb4510dbd"
	};

	// Customer information. We need this values to update the messages
	// we have three options:
	// 1. Create a new customer with random names and store them in the disk
	// 2. Use {firstName}.{lastName}@me.com as the email address and store that (it makes sense if need to log in customers)
	// 3. Hardcode the values here
	private static readonly string FirstName = "Watoshi";
	private static readonly string LastName = "Sabimoto";

	public BuyAnythingClient(ShopWareApiClient apiClient)
	{
		ApiClient = apiClient;
	}

	private ShopWareApiClient ApiClient { get; }
	private List<CachedCountry>? _countries { get; set; }

	// Creates a new "conversation" (or Request). This means that we have to:
	// 1. Create a dummy customer
	// 2. Create a shopping cart for the customer
	// 3. Add an item to the shopping cart (The service to request)
	// 4. Generate an order by checking out the shopping cart and adding a customer comment to it.
	public async Task CreateNewConversationAsync(string emailAddress, string password, string countryId, Product product, string comment, CancellationToken cancellationToken)
	{
		// Messages to use
		var customerRegistrationRequest = ShopWareRequestFactory.CustomerRegistrationRequest(
			FirstName, LastName, emailAddress, password, comment);
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
	}

	public async Task UpdateConversationAsync(NetworkCredential credential, string rawText, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		await ApiClient.UpdateCustomerProfileAsync(ctxToken, ShopWareRequestFactory.CustomerProfileUpdateRequest(FirstName, LastName, rawText), cancellationToken).ConfigureAwait(false);
	}

	public async Task SetBillingAddressAsync(NetworkCredential credential, string address, string houseNumber, string zipCode, string city, string countryId, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var request = ShopWareRequestFactory.BillingAddressRequest(address, houseNumber, zipCode,  city,  countryId );
		await ApiClient.UpdateCustomerBillingAddressAsync(ctxToken, request, cancellationToken).ConfigureAwait(false);
	}

	public async Task<Order[]> GetConversationsUpdateSinceAsync(NetworkCredential credential, DateTimeOffset lastUpdate, CancellationToken cancellationToken)
	{
		var ctxToken = await LoginAsync(credential, cancellationToken).ConfigureAwait(false);
		var orderList = await ApiClient.GetOrderListAsync(ctxToken, cancellationToken).ConfigureAwait(false);
		var updatedOrders = orderList.Orders.Elements
			.Where(o => o.UpdatedAt is not null)
			.Where(o => o.UpdatedAt > lastUpdate)
			.ToArray();
		return updatedOrders;
	}

	private async Task<string> LoginAsync(NetworkCredential credential, CancellationToken cancellationToken)
	{
		var request = ShopWareRequestFactory.CustomerLoginRequest(credential.UserName, credential.Password);
		var response = await ApiClient.LoginCustomerAsync("new-context", request, cancellationToken).ConfigureAwait(false);
		return response.ContextToken;
	}
}
