using NBitcoin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.BuyAnything;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BuyAnything;

public class BuyAnythingManagerTests
{
	//[Fact]
	public async Task BuyAnythingManagerTestAsync()
	{
#if !USE_MOCK
		var shopWareApiClient = PreconfiguredShopWareApiClient();
		shopWareApiClient.OnGenerateOrderAsync = (s, bag) => Task.FromResult(new OrderGenerationResponse("12345", "order#123456789"));
		shopWareApiClient.OnGetCustomerProfileAsync = s => Task.FromResult(
			new CustomerProfileResponse(
				new ChatField("||#WASABI#Hi, I want to by this||#SIB#Bye||"),
				"ctxToken", "customer#", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddSeconds(1)));
		shopWareApiClient.OnGetOrderListAsync = (s, bag) => Task.FromResult(new GetOrderListResponse(new OrderList(new[]
		{
			new Order("1", DateTimeOffset.Now, DateTimeOffset.Now.AddHours(1),
				"10000",
				new StateMachineState(DateTimeOffset.Now, "Open", "Open"),
				new Deliveries[]{new Deliveries("order#123456789",null,new StateMachineState(DateTimeOffset.Now, "Open", "Open")) },
				"order#123456789",
				null,
				new [] { new LineItem(1.0f, "Best Lambo ever", 10000.0f, 10000.0f)},
				"idxxxxxxx",
				new OrderCustomFields(
					"Open", "Link1 | link2", "", "", "", "", "", false, false),
				null,
				new("100"))
		})));
#else
		using var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var http = new ClearnetHttpClient(httpClient);
		ShopWareApiClient shopWareApiClient = new(http, "SWSCU3LIYWVHVXRVYJJNDLJZBG");
#endif
		using WalletWasabi.Wallets.Wallet wallet = new("", Network.Main, KeyManager.CreateNew(out _, "pass", Network.Main, ""));
		//await IoHelpers.TryDeleteDirectoryAsync("datadir");
		var buyAnythingClient = new BuyAnythingClient(shopWareApiClient);
		using var buyAnythingManager = new BuyAnythingManager(Common.DataDir, TimeSpan.FromSeconds(2), buyAnythingClient);
		await buyAnythingManager.StartAsync(CancellationToken.None);

		var argentina = Assert.Single(buyAnythingManager.Countries, x => x.Name == "Argentina");
		var stateId = "none";

		// await buyAnythingManager.StartNewConversationAsync("walletID", argentina.Id, BuyAnythingClient.Product.ConciergeRequest, "Hi, I want to buy this", CancellationToken.None);
		var conversations = await buyAnythingManager.GetConversationsAsync(wallet, CancellationToken.None);
		var conversation = conversations.Last(); // Assert.Single(conversations);
												 //var message = Assert.Single(conversation.ChatMessages);

		conversations = await buyAnythingManager.GetConversationsAsync(wallet, CancellationToken.None);
		conversation = Assert.Single(conversations);

		while (conversation.ConversationStatus != ConversationStatus.OfferReceived)
		{
			await Task.Delay(1000);
			conversation = await buyAnythingManager.GetConversationByIdAsync(conversation.Id, CancellationToken.None);
		}

		await buyAnythingManager.AcceptOfferAsync(conversation, CancellationToken.None);

		while (conversation.ConversationStatus != ConversationStatus.PaymentConfirmed)
		{
			await Task.Delay(1000);
			conversation = await buyAnythingManager.GetConversationByIdAsync(conversation.Id, CancellationToken.None);
		}

		while (conversation.ConversationStatus != ConversationStatus.Shipped)
		{
			await Task.Delay(1000);
			conversation = await buyAnythingManager.GetConversationByIdAsync(conversation.Id, CancellationToken.None);
		}

		await buyAnythingManager.UpdateConversationAsync(conversation, CancellationToken.None);
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
