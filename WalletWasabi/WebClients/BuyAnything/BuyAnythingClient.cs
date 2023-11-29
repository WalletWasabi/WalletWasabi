using System.Collections.Generic;
using System.Linq;
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


	private static readonly string CountriesPath = "./Data/Countries.json";

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
	public async Task<string> CreateNewConversationAsync(string countryId, Product product, string comment, CancellationToken cancellationToken)
	{
		// Messages to use
		var customerRegistrationRequest = CreateRandomCustomer(comment);
		var shoppingCartCreationRequest = new ShoppingCartCreationRequest("My shopping cart");
		var shoppingCartItemAdditionRequest = CreateShoppingCartItemAdditionRequest(ProductIds[product]);
		var orderGenerationRequest = CreateOrderGenerationRequest();

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

		return ctxToken; // return the order number and the token to identify the conversation
	}

	public async Task UpdateConversationAsync(string ctxToken, string rawText)
	{
		Dictionary<string, string> fields = new() { ["wallet_chat_store"] = rawText };
		await ApiClient.UpdateCustomerProfileAsync(ctxToken, new CustomerProfileUpdateRequest(FirstName, LastName, fields), CancellationToken.None).ConfigureAwait(false);
	}

	public async Task<Order[]> GetConversationsUpdateSinceAsync(string ctxToken, DateTimeOffset lastUpdate, CancellationToken cancellationToken)
	{
		var orderList = await ApiClient.GetOrderListAsync(ctxToken, cancellationToken).ConfigureAwait(false);
		var updatedOrders = orderList.Orders.Elements
			.Where(o => o.UpdatedAt is not null)
			.Where(o => o.UpdatedAt > lastUpdate)
			.ToArray();
		return updatedOrders;
	}

	// Creates a non-random customer creation request.
	// There are two kind of customers: Guest and Registered.
	// Guest customers are passwordless can share the same email address and do not have an account.
	// Registered customers have an account and need to call the login API with their credentials (email address and password).
	// Here we create a Guest customer. We assume that the context token is enough to identify the user and that the token
	// doesn't expire. In case this is not true then we need to create a registered customer with random credentials, store
	// them in a file and use them to login the user.
	private CustomerRegistrationRequest CreateRandomCustomer(string message) =>
		new CustomerRegistrationRequest(
			SalutationId: "018b6635785b70679f479eadf50330f3",
			FirstName: FirstName,
			LastName: LastName,
			Email: "emilia.carranza@me.com",
			Password: "Password",
			Guest: false,
			AffiliateCode: "WASABI",
			AcceptedDataProtection: true,
			StorefrontUrl: "https://wasabi.shopinbit.com",
			CustomFields: new() { ["wallet_chat_store"] = $"||#WASABI#{message}||"},
			BillingAddress: new BillingAddress(
				Street: "My street",
				AdditionalAddressLine1: "My additional address line 1",
				Zipcode: "12345",
				City: "Appleton",
				CountryId: "5d54dfdc2b384a8e9fff2bfd6e64c186"
			));

	// Creates a request to add a product to the shopping cart.
	// This product is one of the three services provided by Concierge
	private ShoppingCartItemsRequest CreateShoppingCartItemAdditionRequest(string productId) =>
		new ShoppingCartItemsRequest(
			Items: new[] {
				new ShoppingCartItem (
					Id: "0",
					ReferencedId: productId,
					Label: "",
					Quantity: 1,
					Type: "product",
					Good: true,
					Description: "",
					Stackable: false,
					Removable: false,
					Modified: false)
				});

	// Creates a request to generate an order. The first conversation text is added as a CurstomerComment.
	private OrderGenerationRequest CreateOrderGenerationRequest() =>
		new(
			CustomerComment: "",
			AffiliateCode: "WASABI",
			CampaignCode: "WASABI");
}
