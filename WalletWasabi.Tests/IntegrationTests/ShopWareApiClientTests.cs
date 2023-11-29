using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ShopWareApiClientTests
{
	[Fact]
	public async Task GenerateOrderAsync()
	{
		//using var handler = new HttpClientHandler
		//{
		//	Proxy = new WebProxy("socks5://127.0.0.1", 9050)
		//};
		//using var httpClient = new HttpClient(handler);
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareApiClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		var customer = await shopWareApiClient.RegisterCustomerAsync("none", new CustomerRegistrationRequest(
			SalutationId: "018b6635785b70679f479eadf50330f3",
			FirstName: "Mariela",
			LastName: "Carranza",
			Email: "emilia.carranza@me.com",
			Password: "Password",
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
			await shopWareApiClient.GetOrCreateShoppingCartAsync(customer.ContextTokens[0], new ShoppingCartCreationRequest("My little shopping cart"), CancellationToken.None);

		var shoppingCartx = await shopWareApiClient.AddItemToShoppingCartAsync(customer.ContextTokens[0],
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
		var order = await shopWareApiClient.GenerateOrderAsync(shoppingCartx.Token, new OrderGenerationRequest(
			CustomerComment: "Customer comment",
			AffiliateCode: "WASABI",
			CampaignCode: "WASABI"), CancellationToken.None);

		var orderList = await shopWareApiClient.GetOrderListAsync(shoppingCartx.Token, CancellationToken.None);
		var uniqueOrder = Assert.Single(orderList.Orders.Elements);
		Assert.Equal(uniqueOrder.OrderNumber, order.OrderNumber);

		var cancelledOrder = await shopWareApiClient.CancelOrderAsync(shoppingCartx.Token, new CancelOrderRequest(uniqueOrder.Id),
			CancellationToken.None);

		Assert.Equal("Cancelled", cancelledOrder.Name);
	}

	[Fact]
	public async Task CanRegisterAndLogInAsync()
	{
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareApiClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		// Register a user.
		var customerRequestWithRandomData = CreateRandomCustomer(out var email, out var password);
		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(customer);

		var ogCustomerNumber = customer.CustomerNumber;
		var ogCustomerToken = customer.ContextTokens[0];
		var ogCustomerId = customer.Id;

		// Login with the new user.
		var loggedInCustomer = await shopWareApiClient.LoginCustomerAsync("none", new CustomerLoginRequest(email, password), CancellationToken.None);
		Assert.Equal(loggedInCustomer.ContextToken, customer.ContextTokens[0]);

		// Register with a new user.
		var newCustomerRequestWithRandomData = CreateRandomCustomer(out var newEmail, out var newPassword);
		var newCustomer = await shopWareApiClient.RegisterCustomerAsync("none", newCustomerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(newCustomer);

		var newCustomerNumber = newCustomer.CustomerNumber;
		var newCustomerToken = newCustomer.ContextTokens[0];
		var newCustomerId = newCustomer.Id;

		// Assert that new user's data isn't the same as the first one.
		Assert.NotEqual(ogCustomerNumber, newCustomerNumber);
		Assert.NotEqual(ogCustomerId, newCustomerId);
		Assert.NotEqual(ogCustomerToken, newCustomerToken);
	}

	[Fact]
	public async Task CanFetchCountriesAsync()
	{
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareApiClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		var countryResponse = await shopWareApiClient.GetCountryByNameAsync("none", new GetCountryRequest(
			Page: 1,
			Limit: 10,
			Filter: new[]
				{
				new Filter(
					Type: "equals",
					Field: "name",
					Value: "Sudan"
					)
				}
			), CancellationToken.None);
		var country = countryResponse.Elements.FirstOrDefault();
		Assert.NotNull(country);
		Assert.Equal("Sudan", country.Name);
		Assert.Equal("094d0bb402e542d7b71ff016c10aff7f", country.Id);

		countryResponse = await shopWareApiClient.GetCountryByNameAsync("none", new GetCountryRequest(
			Page: 1,
			Limit: 10,
			Filter: new[]
				{
				new Filter(
					Type: "equals",
					Field: "name",
					Value: "Hungary"
					)
				}
			), CancellationToken.None);

		country = countryResponse.Elements.FirstOrDefault();
		Assert.NotNull(country);
		Assert.Equal("Hungary", country.Name);
		Assert.Equal("6ab3247e27174ee898a2479071754912", country.Id);
	}

	private CustomerRegistrationRequest CreateRandomCustomer(out string email, out string password)
	{
		var crr = new CustomerRegistrationRequest(
			SalutationId: "018b6635785b70679f479eadf50330f3",
			FirstName: "Random",
			LastName: "Dude Jr.",
			Email: $"{Guid.NewGuid()}@me.com",
			Password: "Password",
			Guest: false,
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
		email = crr.Email;
		password = crr.Password;
		return crr;
	}
}
