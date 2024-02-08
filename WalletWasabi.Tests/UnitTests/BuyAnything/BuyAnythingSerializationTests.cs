using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BuyAnything;

public class BuyAnythingSerializationTests
{
	[Fact]
	public void BuyAnythingSerializationTest()
	{
		ConversationTracking conversationTracking = new();

		var conversation = new Conversation(
			new ConversationId("myWalletId", "myUserName", "myPassword", "myOrderId", "myOrderNumber"),
			Chat.Empty,
			OrderStatus.Open,
			ConversationStatus.Started,
			new ConversationMetaData($"Order whatever"));

		var track = new ConversationUpdateTrack(conversation);
		conversationTracking.Add(track);

		List<LineItem> lineItems = new()
		{
			new LineItem(0f, "lineitem 1", 20f, 51f),
			new LineItem(0f, "lineitem 2", 30f, 51f)
		};

		var updatedConversation = track.Conversation
			.AddSystemChatLine("my offer arrive", new OfferCarrier(lineItems.Select(x => new OfferItem(x.Quantity, x.Label, x.UnitPrice, x.TotalPrice)), new ShippingCosts("599")), ConversationStatus.OfferReceived)
			.AddSystemChatLine("my tracking arrived", new TrackingCodes(new[] { "www.mytacking.com/44343", "www.mytacking.com/1234" }), ConversationStatus.PaymentConfirmed);

		updatedConversation = track.Conversation.AddUserMessage("My sent message").AddBotMessage("My received message", DataCarrier.NoData);

		track.Conversation = updatedConversation;
		track.LastUpdate = DateTimeOffset.Now;

		JsonSerializerSettings settings = new()
		{
			TypeNameHandling = TypeNameHandling.Objects
		};

		string json = JsonConvert.SerializeObject(conversationTracking, Formatting.Indented, settings);

		var reLoadedConversation = JsonConvert.DeserializeObject<ConversationTracking>(json, settings) ?? new();
		ConversationTracking reLoadedconversationTracking = new();
		reLoadedconversationTracking.Load(reLoadedConversation);

		Assert.Equal(reLoadedconversationTracking.Conversations.Count, conversationTracking.Conversations.Count);
	}
}
