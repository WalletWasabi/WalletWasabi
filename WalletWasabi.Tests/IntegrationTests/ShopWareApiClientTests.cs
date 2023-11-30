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

		var customerRegistrationRequest = ShopWareRequestFactory.CustomerRegistrationRequest(
			"Lucas", "Carvalho", $"{Guid.NewGuid()}@me.com", "Password", "comment");
		customerRegistrationRequest["guest"] = true;

		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRegistrationRequest, CancellationToken.None);

		var shoppingCart = await shopWareApiClient.GetOrCreateShoppingCartAsync(customer.ContextTokens[0],
			ShopWareRequestFactory.ShoppingCartCreationRequest("My little shopping cart"), CancellationToken.None);
		var shoppingCartx = await shopWareApiClient.AddItemToShoppingCartAsync(customer.ContextTokens[0],
			ShopWareRequestFactory.ShoppingCartItemsRequest("018c0cec5299719f9458dba04f88eb8c") , CancellationToken.None);
		var order = await shopWareApiClient.GenerateOrderAsync(shoppingCartx.Token,
			ShopWareRequestFactory.OrderGenerationRequest(), CancellationToken.None);

		var orderList = await shopWareApiClient.GetOrderListAsync(shoppingCartx.Token, CancellationToken.None);
		var uniqueOrder = Assert.Single(orderList.Orders.Elements);
		Assert.Equal(uniqueOrder.OrderNumber, order.OrderNumber);

		var cancelledOrder = await shopWareApiClient.CancelOrderAsync(shoppingCartx.Token,
			ShopWareRequestFactory.CancelOrderRequest(uniqueOrder.Id), CancellationToken.None);

		Assert.Equal("Cancelled", cancelledOrder.Name);
	}

	[Fact]
	public async Task CanRegisterAndLogInAsync()
	{
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareApiClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		// Register a user.
		var customerRequestWithRandomData = CreateRandomCustomer("a comment", out var email, out var password);
		var customer = await shopWareApiClient.RegisterCustomerAsync("none", customerRequestWithRandomData, CancellationToken.None);
		Assert.NotNull(customer);

		var ogCustomerNumber = customer.CustomerNumber;
		var ogCustomerToken = customer.ContextTokens[0];
		var ogCustomerId = customer.Id;

		// Login with the new user.
		var loggedInCustomer = await shopWareApiClient.LoginCustomerAsync("none", ShopWareRequestFactory.CustomerLoginRequest(email, password), CancellationToken.None);
		Assert.Equal(loggedInCustomer.ContextToken, customer.ContextTokens[0]);

		// Register with a new user.
		var newCustomerRequestWithRandomData = CreateRandomCustomer("no comments", out var newEmail, out var newPassword);
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
	public async Task FetchAllCountriesAsync()
	{
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var shopWareApiClient = new ShopWareApiClient(httpClient, "SWSCU3LIYWVHVXRVYJJNDLJZBG");

		var toSerialize = new List<object>();
		var currentPage = 0;
		while(true)
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
