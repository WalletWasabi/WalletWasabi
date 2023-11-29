using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.BuyAnything;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

// Event that is raised when we detect an update in the server
public record ConversationUpdateEvent(string ConversationId, DateTimeOffset LastUpdate, IEnumerable<ChatMessage> ChatMessages, Wallet Wallet);

public record ChatMessage(bool IsMyMessage, string Message);

public class Conversation
{
	public string CustomerEmail { get; set; }
	public string CustomerPassword { get; set; }

	// public string ContextToken { get; }
	public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
}

// Class to keep a track of the last update of a conversation
public class ConversationUpdateTrack
{
	public ConversationUpdateTrack(string contextToken, Wallet wallet)
	{
		Wallet = wallet;
		ContextToken = contextToken;
	}

	public string ContextToken { get; }
	public DateTimeOffset LastUpdate { get; set; }
	public Conversation Conversation { get; set; } = new();
	public Wallet Wallet { get; }
}

// Class to manage the conversation updates
// This is a toy implementation just to share the idea by code.
public class BuyAnythingManager : PeriodicRunner
{
	public BuyAnythingManager(TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
	}

	private BuyAnythingClient Client { get; }
	private List<ConversationUpdateTrack> Conversations { get; } = new();

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		foreach (var track in Conversations)
		{
			var orders = track.ContextToken != "myDebugConversation"
				? await Client.GetConversationsUpdateSinceAsync(track.ContextToken, track.LastUpdate, cancel).ConfigureAwait(false)
				: GetDummyOrders();

			foreach (var order in orders.Where(o => o.UpdatedAt!.Value > track.LastUpdate))
			{
				var orderLastUpdated = order.UpdatedAt!.Value;
				track.LastUpdate = orderLastUpdated;
				var newMessageFromConcierge = Parse(order.CustomerComment);
				ConversationUpdated.SafeInvoke(this, new ConversationUpdateEvent(track.ContextToken, orderLastUpdated, newMessageFromConcierge, track.Wallet));
			}
		}
	}

	private IEnumerable<ChatMessage> Parse(string customerComment)
	{
		if (customerComment is null)
		{
			return Enumerable.Empty<ChatMessage>();
		}

		var messages = customerComment.Split("||", StringSplitOptions.RemoveEmptyEntries);
		if (!messages.Any())
		{
			return Enumerable.Empty<ChatMessage>();
		}

		List<ChatMessage> chatMessages = new();

		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				return Enumerable.Empty<ChatMessage>();
			}

			var isMine = items[0] == "WASABI";
			var text = items[1];
			chatMessages.Add(new ChatMessage(isMine, text));
		}

		return chatMessages.ToArray();
	}

	private string ConvertToCustomerComment(IEnumerable<ChatMessage> cleanChatMessages)
	{
		StringBuilder result = new();

		foreach (var chatMessage in cleanChatMessages)
		{
			if (chatMessage.IsMyMessage)
			{
				result.Append($"||#WASABI#{chatMessage.Message}");
			}
			else
			{
				result.Append($"||#SIB#{chatMessage.Message}");
			}
		}

		result.Append("||");

		return result.ToString();
	}

	public void SendAndUpdateConversation(ChatMessage newMessage, string customerEmail, string customerPassword)
	{
		// Update local cache.
		ConversationUpdateTrack conversationToUpdate = Conversations.First(conv => conv.Conversation.CustomerEmail == customerEmail && conv.Conversation.CustomerPassword == customerPassword);
		conversationToUpdate.Conversation.Messages = conversationToUpdate.Conversation.Messages.Concat([newMessage]).ToArray(); // Why array instead of List?
		conversationToUpdate.LastUpdate = DateTimeOffset.Now;

		// Convert conversation messages to customer comment.
		var sendableCustomerComment = ConvertToCustomerComment(conversationToUpdate.Conversation.Messages);

		// Send whole conversation to SIB.
		//Client.SendNewMessage(sendableCustomerComment, customerEmail, customerPassword);
	}

	public IEnumerable<Conversation> GetConversations(Wallet wallet)
	{
		return Conversations.Where(c => c.Wallet == wallet).Select(c => c.Conversation);
	}

	public void AddConversationsFromWallet(Wallet wallet)
	{
		// Feed the dummy data.
		Conversations.Add(new ConversationUpdateTrack("myDebugConversation", wallet));
	}

	private Order OrderUntilCountry { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#SIB#I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.||" +
		"||#SIB#I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount" +
		"||#SIB#Let's begin by selecting your country." +
		"||#WASABI#Germany", null, null, null, null, null, null);

	private Order OrderUntilProduct { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#SIB#I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.||" +
		"||#SIB#I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount" +
		"||#SIB#Let's begin by selecting your country." +
		"||#WASABI#Germany" +
		"||#SIB#What would you like to buy?" +
		"||#WASABI#I would like to have a Lambo with manual transmission." +
		"||#SIB#We've received your request, we will be in touch with you within the next couple of days." +
		"||#SIB#What color would you prefer?" +
		"||#WASABI#Wasabi Green please!" +
		"||#WASABI#Ohh and sorry, I changed my mind, I would like to have one with a automatic transmission." +
		"||#SIB#Recorded your modifications, thank you!", null, null, null, null, null, null);

	private Order OrderUntilShipping { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#SIB#I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.||" +
		"||#SIB#I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount" +
		"||#SIB#Let's begin by selecting your country." +
		"||#WASABI#Germany" +
		"||#SIB#What would you like to buy?" +
		"||#WASABI#I would like to have a Lambo with manual transmission." +
		"||#SIB#We've received your request, we will be in touch with you within the next couple of days." +
		"||#SIB#What color would you prefer?" +
		"||#WASABI#Wasabi Green please!" +
		"||#WASABI#Ohh and sorry, I changed my mind, I would like to have one with a automatic transmission." +
		"||#SIB#Recorded your modifications, thank you!" +
		"||#SIB#I can offer you an automatic Green Lambo for delivery to Germany by December 24, 2023, at the cost of 300,000 USD or approximately 10.5 BTC." +
		"||#SIB#To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:" +
		"||#SIB#Your First Name:" +
		"||#WASABI#Satoshi" +
		"||#SIB#Your Last Name:" +
		"||#WASABI#Nakamoto" +
		"||#SIB#Street Name:" +
		"||#WASABI#Top secret street" +
		"||#SIB#House Number:" +
		"||#WASABI#25" +
		"||#SIB#ZIP/Postal Code:" +
		"||#WASABI#10115" +
		"||#SIB#City:" +
		"||#WASABI#Berlin" +
		"||#SIBState:#" +
		"||#WASABI#State of Berlin" +
		"||#SIB#Thank you for the information. Please take a moment to verify the accuracy of the provided data. If any details are incorrect, you can make adjustments using the \"EDIT\" button,if everything is correct, click “PLACE ORDER” and accept Terms and Conditions." +
		"||#WASABI#www.termsandconditions.com", null, null, null, null, null, null);

	private Order OrderUntilPayment { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#SIB#I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.||" +
		"||#SIB#I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount" +
		"||#SIB#Let's begin by selecting your country." +
		"||#WASABI#Germany" +
		"||#SIB#What would you like to buy?" +
		"||#WASABI#I would like to have a Lambo with manual transmission." +
		"||#SIB#We've received your request, we will be in touch with you within the next couple of days." +
		"||#SIB#What color would you prefer?" +
		"||#WASABI#Wasabi Green please!" +
		"||#WASABI#Ohh and sorry, I changed my mind, I would like to have one with a automatic transmission." +
		"||#SIB#Recorded your modifications, thank you!" +
		"||#SIB#I can offer you an automatic Green Lambo for delivery to Germany by December 24, 2023, at the cost of 300,000 USD or approximately 10.5 BTC." +
		"||#SIB#To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:" +
		"||#SIB#Your First Name:" +
		"||#WASABI#Satoshi" +
		"||#SIB#Your Last Name:" +
		"||#WASABI#Nakamoto" +
		"||#SIB#Street Name:" +
		"||#WASABI#Top secret street" +
		"||#SIB#House Number:" +
		"||#WASABI#25" +
		"||#SIB#ZIP/Postal Code:" +
		"||#WASABI#10115" +
		"||#SIB#City:" +
		"||#WASABI#Berlin" +
		"||#SIBState:#" +
		"||#WASABI#State of Berlin" +
		"||#SIB#Thank you for the information. Please take a moment to verify the accuracy of the provided data. If any details are incorrect, you can make adjustments using the \"EDIT\" button,if everything is correct, click “PLACE ORDER” and accept Terms and Conditions." +
		"||#WASABI#www.termsandconditions.com" +
		"||#WASABI#Everything is correct and I accept the terms and conditions." +
		"||#SIB#To finalize your order, kindly transfer 10.5 BTC to the following address:" +
		"||#SIB#bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfixOwlh" +
		"||#SIB#Once your payment is confirmed, we'll initiate the delivery process." +
		"||#SIB#", null, null, null, null, null, null);

	private Order FullOrder { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#SIB#I'm here to assist you with anything you need to buy. Whether it's flights, cars, or any other request, just let me know, and I'll take care of it for you.||" +
		"||#SIB#I'd like to kindly inform you that our minimum transaction amount is $1,000 USD. Please feel free to share any requests above this amount" +
		"||#SIB#Let's begin by selecting your country." +
		"||#WASABI#Germany" +
		"||#SIB#What would you like to buy?" +
		"||#WASABI#I would like to have a Lambo with manual transmission." +
		"||#SIB#We've received your request, we will be in touch with you within the next couple of days." +
		"||#SIB#What color would you prefer?" +
		"||#WASABI#Wasabi Green please!" +
		"||#WASABI#Ohh and sorry, I changed my mind, I would like to have one with a automatic transmission." +
		"||#SIB#Recorded your modifications, thank you!" +
		"||#SIB#I can offer you an automatic Green Lambo for delivery to Germany by December 24, 2023, at the cost of 300,000 USD or approximately 10.5 BTC." +
		"||#SIB#To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:" +
		"||#SIB#Your First Name:" +
		"||#WASABI#Satoshi" +
		"||#SIB#Your Last Name:" +
		"||#WASABI#Nakamoto" +
		"||#SIB#Street Name:" +
		"||#WASABI#Top secret street" +
		"||#SIB#House Number:" +
		"||#WASABI#25" +
		"||#SIB#ZIP/Postal Code:" +
		"||#WASABI#10115" +
		"||#SIB#City:" +
		"||#WASABI#Berlin" +
		"||#SIBState:#" +
		"||#WASABI#State of Berlin" +
		"||#SIB#Thank you for the information. Please take a moment to verify the accuracy of the provided data. If any details are incorrect, you can make adjustments using the \"EDIT\" button,if everything is correct, click “PLACE ORDER” and accept Terms and Conditions." +
		"||#WASABI#www.termsandconditions.com" +
		"||#WASABI#Everything is correct and I accept the terms and conditions." +
		"||#SIB#To finalize your order, kindly transfer 10.5 BTC to the following address:" +
		"||#SIB#bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfixOwlh" +
		"||#SIB#Once your payment is confirmed, we'll initiate the delivery process." +
		"||#SIB#Great news! Your order is complete." +
		"||#SIB#Download your files:" +
		"||#SIB#www.invoice.com/lamboincoice" +
		"||#SIB#For shipping updates:" +
		"||#SIB#www.deliverycompany.com/trcknmbr0000000001" +
		"||#SIB#This conversation will vanish in 30 days, make sure to save all the important info beforehand.", null, null, null, null, null, null);

	private Order[] GetDummyOrders()
	{
		return new[]
		{
			OrderUntilCountry,
			OrderUntilProduct,
			OrderUntilPayment,
			FullOrder
		};
	}
}
