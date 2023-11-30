using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.IntegrationTests;

public class ShopWareApiClientTests
{
	private readonly HttpClientFactory _httpClientFactory = new (null, null); // Tor not used for tests
	[Fact]
	public async Task GenerateOrderAsync()
	{
		var shopWareApiClient = new ShopWareApiClient(_httpClientFactory, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		var customerName = "Lucas";
		var customerLastName = "Carvalho";
		var customerEmail = $"{Guid.NewGuid()}@me.com";
		var customerPassword = $"Password";
		var customerRegistrationRequest = ShopWareRequestFactory.CustomerRegistrationRequest(
			customerName, customerLastName, customerEmail, customerPassword, "comment");
		customerRegistrationRequest["guest"] = true;

		var customerExternal = await shopWareApiClient.RegisterCustomerAsync(customerRegistrationRequest, CancellationToken.None);
		var localCustomer = new LocalCustomer(customerExternal.Id, customerExternal.CustomerNumber, customerEmail,
			customerPassword, customerExternal.ContextTokens[0]);

		var shoppingCart = await shopWareApiClient.GetOrCreateShoppingCartAsync(localCustomer,
			ShopWareRequestFactory.ShoppingCartCreationRequest("My little shopping cart"), CancellationToken.None);
		var shoppingCartx = await shopWareApiClient.AddItemToShoppingCartAsync(localCustomer,
			ShopWareRequestFactory.ShoppingCartItemsRequest("018c0cec5299719f9458dba04f88eb8c") , CancellationToken.None);
		var order = await shopWareApiClient.GenerateOrderAsync(localCustomer,
			ShopWareRequestFactory.OrderGenerationRequest(), CancellationToken.None);

		var orderList = await shopWareApiClient.GetOrderListAsync(localCustomer, CancellationToken.None);
		var uniqueOrder = Assert.Single(orderList.Orders.Elements);
		Assert.Equal(uniqueOrder.OrderNumber, order.OrderNumber);

		var cancelledOrder = await shopWareApiClient.CancelOrderAsync(localCustomer,
			ShopWareRequestFactory.CancelOrderRequest(uniqueOrder.Id), CancellationToken.None);

		Assert.Equal("Cancelled", cancelledOrder.Name);
	}

	[Fact]
	public async Task CanRegisterAndLogInAsync()
	{
		var shopWareApiClient = new ShopWareApiClient(_httpClientFactory, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		// Register a user.
		var customerRequestWithRandomData = CreateRandomCustomer("a comment", out var email, out var password);
		var customer = await shopWareApiClient.RegisterCustomerAsync(customerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(customer);

		var ogLocalCustomer = new LocalCustomer(
			customer.Id,
			customer.CustomerNumber,
			email,
			password,
			customer.ContextTokens[0]);

		// Login with the new user.
		var loggedInCustomer = await shopWareApiClient.LoginCustomerAsync(ogLocalCustomer, ShopWareRequestFactory.CustomerLoginRequest(email, password), CancellationToken.None);
		Assert.Equal(loggedInCustomer.ContextToken, customer.ContextTokens[0]);

		// Register with a new user.
		var newCustomerRequestWithRandomData = CreateRandomCustomer("no comments", out var newEmail, out var newPassword);
		var newCustomer = await shopWareApiClient.RegisterCustomerAsync(newCustomerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(newCustomer);

		var newLocalCustomer = new LocalCustomer(
			newCustomer.Id,
			newCustomer.CustomerNumber,
			newEmail,
			newPassword,
			newCustomer.ContextTokens[0]);

		// Assert that new user's data isn't the same as the first one.
		Assert.NotEqual(ogLocalCustomer.CustomerNumber, newLocalCustomer.CustomerNumber);
		Assert.NotEqual(ogLocalCustomer.Id, newLocalCustomer.Id);
		Assert.NotEqual(ogLocalCustomer.LastKnownAccessToken, newLocalCustomer.LastKnownAccessToken);
	}

	[Fact]
	public async Task FetchAllCountriesAsync()
	{
		var shopWareApiClient = new ShopWareApiClient(_httpClientFactory, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		var toSerialize = new List<object>();
		var currentPage = 0;
		while(true)
		{
			currentPage++;

			var countryResponse = await shopWareApiClient.GetCountriesAsync(ShopWareRequestFactory.GetPage(currentPage, 100), CancellationToken.None);
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

		// Save the new file if it changed
		// var outputFolder = Directory.CreateDirectory(Common.GetWorkDir(nameof(ShopWareApiClient), "ShopWareApiClient"));
		// await File.WriteAllTextAsync(Path.Combine(outputFolder.FullName, "Countries.json"), JsonConvert.SerializeObject(toSerialize));
	}

	private PropertyBag CreateRandomCustomer(string message, out string email, out string password)
	{
		PropertyBag crr = ShopWareRequestFactory.CustomerRegistrationRequest(
			firstName: "Random",
			lastName: "Dude Jr.",
			email: $"{Guid.NewGuid()}@me.com",
			password: "Password",
			message: message);
		email = crr["email"].ToString();
		password = crr["password"].ToString();
		return crr;
	}
}
