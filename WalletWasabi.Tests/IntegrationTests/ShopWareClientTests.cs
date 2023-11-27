using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ShopWareClientTests
{
	[Fact]
	public async Task GenerateOrderAsync()
	{
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");
		var customer = await shopWareClient.RegisterCustomerAsync("none", new CustomerRegistrationRequest(
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
			)), CancellationToken.None);

		var shoppingCart =
			await shopWareClient.GetOrCreateShoppingCartAsync(customer.ContextTokens[0], new ShoppingCartCreationRequest("My little shopping cart"), CancellationToken.None);
		var shoppingCartx = await shopWareClient.AddItemToShoppingCartAsync(customer.ContextTokens[0],
			new ShoppingCartItemsRequest(
				Items: new[]
				{
					new ShoppingCartItem(
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
				}), CancellationToken.None);
		var order = await shopWareClient.GenerateOrderAsync(shoppingCartx.Token, new OrderGenerationRequest(
			CustomerComment: "Customer comment",
			AffiliateCode: "WASABI",
			CampaignCode: "WASABI"), CancellationToken.None);

		Assert.StartsWith("SIB-", order.OrderNumber);
	}
}
