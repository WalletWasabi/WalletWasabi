using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Helpers;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BuyAnything;

public class BuyAnythingManagerTests
{
	[Fact]
	public async Task BuyAnythingManagerTest()
	{
#if !USE_MOCK
		var shopWareApiClient = PreconfiguredShopWareApiClient();
		shopWareApiClient.OnGenerateOrderAsync = (s, bag) => Task.FromResult(new OrderGenerationResponse("12345","order#123456789"));
		shopWareApiClient.OnGetCustomerProfileAsync = s => Task.FromResult(
			new CustomerProfileResponse(
				new ChatField("||#WASABI#Hi, I want to by this||#SIB#Bye||"),
				"ctxToken", "customer#", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddSeconds(1)));
		shopWareApiClient.OnGetOrderListAsync = (s, bag) => Task.FromResult(new GetOrderListResponse(new OrderList(new[]
		{
			new Order("1", DateTimeOffset.Now, DateTimeOffset.Now.AddHours(1),
				new StateMachineState(DateTimeOffset.Now, "Open", "Open"),
				"order#123456789",
				null,
				new LineItem[0],
				"idxxxxxxx",
				null,
				null)
		})));
#else
		var shopWareApiClient = new ShopWareApiClient(new HttpClient(), "real-api-key");
#endif

		await IoHelpers.TryDeleteDirectoryAsync("datadir");
		var buyAnythingClient = new BuyAnythingClient(shopWareApiClient);
		using var buyAnythingManager = new BuyAnythingManager("datadir", TimeSpan.FromDays(7), buyAnythingClient);
		await buyAnythingManager.StartAsync(CancellationToken.None);

		var countries = await buyAnythingManager.GetCountriesAsync(CancellationToken.None);
		Assert.Single(countries, x => x.Name == "Argentina");

		await buyAnythingManager.StartNewConversationAsync("walletID", BuyAnythingClient.Product.ConciergeRequest, "Hi, I want to buy this", CancellationToken.None);
		var conversations = await buyAnythingManager.GetConversationsAsync("walletID", CancellationToken.None);
		var conversation = Assert.Single(conversations);
		var message = Assert.Single(conversation.ChatMessages);
		Assert.Equal("Hi, I want to buy this", message.Message);

		await buyAnythingManager.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(1));

		conversations = await buyAnythingManager.GetConversationsAsync("walletID", CancellationToken.None);
		conversation = Assert.Single(conversations);
		var reply = Assert.Single(conversation.ChatMessages, m => !m.IsMyMessage);
		Assert.Equal("Bye", reply.Message);

		await buyAnythingManager.UpdateConversationAsync(conversation.Id, "Ok Bye", "metadata", CancellationToken.None);
		conversations = await buyAnythingManager.GetConversationsAsync("walletID", CancellationToken.None);
		conversation = Assert.Single(conversations);
		Assert.Equal(3, conversation.ChatMessages.Count);
		var myMessages = conversation.ChatMessages.Where(m => m.IsMyMessage).ToArray();
		Assert.Equal("Ok Bye", myMessages[1].Message);

		// Parse testing
		var conversationString = "||#WASABI#Hi, I want to by this||#SIB#Bye||#WASABI#Ok Bye||";
		var text = Chat.FromText(conversationString).ToText();
		Assert.Equal(conversationString, text);
	}

	private MockShopWareApiClient PreconfiguredShopWareApiClient()
	{
		var mockedShopwareClient = new MockShopWareApiClient();
		mockedShopwareClient.OnLoginCustomerAsync = (s, bag) =>
			Task.FromResult(new CustomerLoginResponse("token-whatever"));

		mockedShopwareClient.OnRegisterCustomerAsync = (s, bag) =>
			Task.FromResult(new CustomerRegistrationResponse("872-xxxx-xxxx", "customer-whatever",
				new[] { "ctx-token-for-872-xxxx-xxxx" }));

		mockedShopwareClient.OnGetOrCreateShoppingCartAsync = (s, bag) =>
			Task.FromResult(new ShoppingCartResponse("ctx-token-for-872-xxxx-xxxx"));

		mockedShopwareClient.OnAddItemToShoppingCartAsync = (s, bag) =>
			Task.FromResult(new ShoppingCartItemsResponse("ctx-token-for-872-xxxx-xxxx"));

		mockedShopwareClient.OnUpdateCustomerProfileAsync = (s, bag) =>
			Task.FromResult(bag);

		return mockedShopwareClient;
	}
}
