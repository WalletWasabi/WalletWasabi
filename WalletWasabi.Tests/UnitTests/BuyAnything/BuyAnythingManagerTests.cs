using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Tor.Http;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare;
using WalletWasabi.WebClients.ShopWare.Models;
using WalletWasabi.WebClients.Wasabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BuyAnything;

public class BuyAnythingManagerTests
{
	[Fact]
	public async Task BuyAnythingManagerTest()
	{
#if USE_MOCK
		var shopWareApiClient = PreconfiguredShopWareApiClient();
		shopWareApiClient.OnGenerateOrderAsync = (s, bag) => Task.FromResult(new OrderGenerationResponse("12345", "order#123456789"));
		shopWareApiClient.OnGetCustomerProfileAsync = s => Task.FromResult(
			new CustomerProfileResponse(
				new ChatField("||#WASABI#Hi, I want to by this||#SIB#Bye||"),
				"ctxToken", "customer#", DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddSeconds(1)));
		shopWareApiClient.OnGetOrderListAsync = (s, bag) => Task.FromResult(new GetOrderListResponse(new OrderList(new[]
		{
			new Order("1", DateTimeOffset.Now, DateTimeOffset.Now.AddHours(1),
				new StateMachineState(DateTimeOffset.Now, "Open", "Open"),
				new Deliveries[]{new Deliveries("order#123456789",null,new StateMachineState(DateTimeOffset.Now, "Open", "Open")) },
				"order#123456789",
				null,
				new [] { new LineItem(1.0f, "Best Lambo ever", 10000.0f, 10000.0f)},
				"idxxxxxxx",
				new OrderCustomFields(
					"Open", "Link1 | link2", "", "", "", "", "", false, false),
				null)
		})));
#else
		var httpClient = new HttpClient();
		httpClient.BaseAddress = new Uri("https://shopinbit.com/store-api/");
		var http = new ClearnetHttpClient(httpClient);
		ShopWareApiClient shopWareApiClient = new(http, "SWSCU3LIYWVHVXRVYJJNDLJZBG");
#endif

		//await IoHelpers.TryDeleteDirectoryAsync("datadir");
		var buyAnythingClient = new BuyAnythingClient(shopWareApiClient);
		using var buyAnythingManager = new BuyAnythingManager("datadir", TimeSpan.FromSeconds(2), buyAnythingClient);
		await buyAnythingManager.StartAsync(CancellationToken.None);

		var countries = await buyAnythingManager.GetCountriesAsync(CancellationToken.None);
		var argentina = Assert.Single(countries, x => x.Name == "Argentina");

		//await buyAnythingManager.StartNewConversationAsync("walletID", argentina.Id, BuyAnythingClient.Product.ConciergeRequest, "Hi, I want to buy this", CancellationToken.None);
		var conversations = await buyAnythingManager.GetConversationsAsync("walletID", CancellationToken.None);
		var conversation = conversations.Last(); // Assert.Single(conversations);
		//var message = Assert.Single(conversation.ChatMessages);

		conversations = await buyAnythingManager.GetConversationsAsync("walletID", CancellationToken.None);
		conversation = Assert.Single(conversations);

		while (conversation.ConversationStatus != ConversationStatus.OfferReceived)
		{
			await Task.Delay(1000);
			conversation = await buyAnythingManager.GetConversationByIdAsync(conversation.Id, CancellationToken.None);
		}

		await buyAnythingManager.AcceptOfferAsync(conversation.Id, "Lucas", "Ontivero", "Carlos III", "12345", "5000",
			"Cordoba", argentina.Id, CancellationToken.None);

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
		await buyAnythingManager.UpdateConversationAsync(conversation.Id, "Ok Bye", "metadata", CancellationToken.None);
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
