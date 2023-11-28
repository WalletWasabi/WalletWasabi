using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.WebClients.BuyAnything;

public class BuyAnythingClient
{
	public BuyAnythingClient(ShopWareApiClient apiClient)
	{
		ApiClient = apiClient;
	}

	private ShopWareApiClient ApiClient { get; }

	public async Task<string> CreateNewConversationAsync(string countryId, string comment, CancellationToken cancellationToken)
	{
		//var countryResponse = await apiClient.GetCountryByNameAsync(countryName, cancellationToken).ConfigureAwait(false);
		var customerRegistrationRequest = CreateRandomCustomer();
		var shoppingCartCreationRequest = new ShoppingCartCreationRequest("My shopping cart");
		var shoppingCartItemAdditionRequest = CreateShoppingCartItemAdditionRequest();
		var orderGenerationRequest = CreateOrderGenerationRequest();
		var customerRegistrationResponse = await ApiClient.RegisterCustomerAsync("new-context",customerRegistrationRequest, cancellationToken).ConfigureAwait(false);
		var shoppingCartCreationResponse = await ApiClient.GetOrCreateShoppingCartAsync(customerRegistrationResponse.ContextTokens[0], shoppingCartCreationRequest, cancellationToken).ConfigureAwait(false);
		var shoppingCartItemAdditionResponse = await ApiClient.AddItemToShoppingCartAsync(shoppingCartCreationResponse.Token, shoppingCartItemAdditionRequest, cancellationToken).ConfigureAwait(false);
		var orderGenerationResponse = await ApiClient.GenerateOrderAsync(shoppingCartItemAdditionResponse.Token, orderGenerationRequest, cancellationToken).ConfigureAwait(false);
		return orderGenerationResponse.OrderNumber;
	}


	public async Task GetUpdatesAsync(string conversationId, CancellationToken cancellationToken)
	{

		//var countryResponse = await apiClient.GetCountryByNameAsync(countryName, cancellationToken).ConfigureAwait(false);
	}

	private CustomerRegistrationRequest CreateRandomCustomer() =>
		new CustomerRegistrationRequest(
			SalutationId: "018b6635785b70679f479eadf50330f3",
			FirstName: "Mariela",
			LastName: "Carranza",
			Email: "emilia.carranza@me.com",
			Guest: true,
			AffiliateCode: "WASABI",
			AcceptedDataProtection: true,
			StorefrontUrl: "https://wasabi.shopinbit.com",
			BillingAddress: new BillingAddress(
				Street: "My street",
				AdditionalAddressLine1: "My additional address line 1",
				Zipcode: "12345",
				City: "Appleton",
				CountryId: "5d54dfdc2b384a8e9fff2bfd6e64c186"
			));

	private ShoppingCartItemsRequest CreateShoppingCartItemAdditionRequest() =>
		new ShoppingCartItemsRequest(
			Items: new[] {
				new ShoppingCartItem (
					Id: "0",
					ReferencedId: "018c0cec5299719f9458dba04f88eb8c",
					Label: "The label",
					Quantity: 1,
					Type: "product",
					Good: true,
					Description: "description",
					Stackable: false,
					Removable: false,
					Modified: false)
				});

	private OrderGenerationRequest CreateOrderGenerationRequest() =>
		new (
			CustomerComment: "no comment",
			AffiliateCode: "WASABI",
			CampaignCode: "WASABI");
}
