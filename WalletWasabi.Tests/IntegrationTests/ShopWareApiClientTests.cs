using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;
using WalletWasabi.Tor.Socks5.Pool.Circuits;
using WalletWasabi.WebClients.Wasabi;
using WalletWasabi.Tor.Http;

namespace WalletWasabi.Tests.IntegrationTests;

public class ShopWareApiClientTests
{
	[Fact]
	public async Task GenerateOrderAsync()
	{
		await using TestSetup testSetup = TestSetup.ForClearnet();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		var customerRegistrationRequest = ShopWareRequestFactory.CustomerRegistrationRequest(
			"018b6635785b70679f479eadf50330f3", "Lucas", "Carvalho", $"{Guid.NewGuid()}@me.com", "Password", "5d54dfdc2b384a8e9fff2bfd6e64c186", "comment", "https://wasabi.shopinbit.com");

		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRegistrationRequest, CancellationToken.None);

		var shoppingCart = await shopWareApiClient.GetOrCreateShoppingCartAsync(customer.ContextTokens[0],
			ShopWareRequestFactory.ShoppingCartCreationRequest("My little shopping cart"), CancellationToken.None);
		var shoppingCartItemsResponse = await shopWareApiClient.AddItemToShoppingCartAsync(customer.ContextTokens[0],
			ShopWareRequestFactory.ShoppingCartItemsRequest("018c0cec5299719f9458dba04f88eb8c"), CancellationToken.None);
		var orderResponse = await shopWareApiClient.GenerateOrderAsync(shoppingCartItemsResponse.Token,
			ShopWareRequestFactory.OrderGenerationRequest(), CancellationToken.None);
		var orderListResponse = await shopWareApiClient.GetOrderListAsync(shoppingCartItemsResponse.Token,
			ShopWareRequestFactory.GetOrderListRequest(), CancellationToken.None);
		var uniqueOrder = Assert.Single(orderListResponse.Orders.Elements);
		Assert.Equal(uniqueOrder.OrderNumber, orderResponse.OrderNumber);

		var cancelledOrder = await shopWareApiClient.CancelOrderAsync(shoppingCartItemsResponse.Token,
			ShopWareRequestFactory.CancelOrderRequest(uniqueOrder.Id), CancellationToken.None);

		Assert.Equal("Cancelled", cancelledOrder.Name);
	}

	[Fact]
	public async Task CanRegisterAndLogInClearnetAsync()
	{
		await using TestSetup testSetup = TestSetup.ForClearnet();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		await CanRegisterAndLogInAsync(shopWareApiClient);
	}

	// Make sure tor.exe is running locally, otherwise the test fails.
	[Fact]
	public async Task CanRegisterAndLogInTorAsync()
	{
		await using TestSetup testSetup = TestSetup.ForTor();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		await CanRegisterAndLogInAsync(shopWareApiClient);
	}

	private async Task CanRegisterAndLogInAsync(ShopWareApiClient shopWareApiClient)
	{
		// Register a user.
		var customerRequestWithRandomData = CreateRandomCustomer("a comment", out var email, out var password);
		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(customer);

		var ogCustomerNumber = customer.CustomerNumber;
		var ogCustomerToken = customer.ContextTokens[0];
		var ogCustomerId = customer.Id;

		// Log in with the new user.
		var loggedInCustomer = await shopWareApiClient.LoginCustomerAsync("none", ShopWareRequestFactory.CustomerLoginRequest(email, password), CancellationToken.None);
		Assert.Equal(loggedInCustomer.ContextToken, customer.ContextTokens[0]);

		// Register with a new user.
		var newCustomerRequestWithRandomData = CreateRandomCustomer("no comments", out _, out _);
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
	public async Task CanFetchCountriesAndStatesAsync()
	{
		await using TestSetup testSetup = TestSetup.ForClearnet();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		var toSerialize = new List<CachedCountry>();
		var currentPage = 0;
		while (true)
		{
			currentPage++;

			var countryResponse = await shopWareApiClient.GetCountriesAsync("none", ShopWareRequestFactory.GetPage(currentPage, 100), CancellationToken.None);
			var cachedCountries = countryResponse.Elements
				.Where(x => x.Active)
				.Select(x => new CachedCountry(
					Id: x.Id,
					Name: x.Name)
				);

			toSerialize.AddRange(cachedCountries);

			if (countryResponse.Total != countryResponse.Limit)
			{
				break;
			}
		}

		// If a country is added or removed, test will fail and we will be notified.
		// We could go further and verify equality.
		Assert.Equal(246, toSerialize.Count);

		var stateResponse = await shopWareApiClient.GetStatesByCountryIdAsync("none", toSerialize.First(c => c.Name == "United States of America").Id, CancellationToken.None);
		Assert.Equal(51, stateResponse.Elements.Count);

		// Save the new file if it changed
		// var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(ShopWareApiClient), "ShopWareApiClient"));
		// await File.WriteAllTextAsync(Path.Combine(outputFolder.FullName, "Countries.json"), JsonSerializer.Serialize(toSerialize));
	}

	[Fact]
	public async Task CanUpdateBillingAddressAsync()
	{
		var customerRequestWithRandomData = CreateRandomCustomer("Set billing address please.", out var email, out var password);

		await using TestSetup testSetup = TestSetup.ForClearnet();
		ShopWareApiClient shopWareApiClient = testSetup.ShopWareApiClient;

		// Register
		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRequestWithRandomData, CancellationToken.None);

		// Login
		var loggedInCustomer = await shopWareApiClient.LoginCustomerAsync(customer.ContextTokens[0], ShopWareRequestFactory.CustomerLoginRequest(email, password), CancellationToken.None);

		// Update billing address
		await shopWareApiClient.UpdateCustomerBillingAddressAsync(
			loggedInCustomer.ContextToken,
			ShopWareRequestFactory.BillingAddressRequest(
			customerRequestWithRandomData["firstName"].ToString()!,
			customerRequestWithRandomData["lastName"].ToString()!,
			"My updated street",
			"123",
			"1022",
			"Budapest",
			"none",
			"6ab3247e27174ee898a2479071754912"),
			CancellationToken.None);

		await Task.Delay(10000);

		// Check admin site if the customer billing address got updated or not.
	}

	private PropertyBag CreateRandomCustomer(string message, out string email, out string password)
	{
		email = $"{Guid.NewGuid()}@me.com";
		password = "Password";

		PropertyBag crr = ShopWareRequestFactory.CustomerRegistrationRequest(
			salutationId: "018b6635785b70679f479eadf50330f3",
			firstName: "Random",
			lastName: "Dude Jr.",
			email: email,
			password: password,
			countryId: "5d54dfdc2b384a8e9fff2bfd6e64c186",
		message: message,
			storefrontUrl: "https://wasabi.shopinbit.com");

		return crr;
	}

	private class TestSetup : IAsyncDisposable
	{
		private TestSetup(bool useTor)
		{
			EndPoint? torEndpoint = useTor ? Common.TorSocks5Endpoint : null;
			HttpClientFactory = new(torEndpoint, null);

			IHttpClient httpClient = useTor
				? HttpClientFactory.NewTorHttpClient(Mode.DefaultCircuit, () => new Uri("https://shopinbit.solution360.dev/store-api/"))
				: HttpClientFactory.NewHttpClient(() => new Uri("https://shopinbit.solution360.dev/store-api/"), Mode.DefaultCircuit);

			ShopWareApiClient = new(httpClient, "SWSCVTGZRHJOZWF0MTJFTK9ZSG");
		}

		private WasabiHttpClientFactory HttpClientFactory { get; }
		public ShopWareApiClient ShopWareApiClient { get; }

		public static TestSetup ForClearnet()
			=> new(useTor: false);

		public static TestSetup ForTor()
			=> new(useTor: true);

		public async ValueTask DisposeAsync()
		{
			await HttpClientFactory.DisposeAsync().ConfigureAwait(false);
		}
	}
}
