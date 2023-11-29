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
	public string ContextToken { get; }
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
				: new[] { MyDummyOrder };

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

	public IEnumerable<Conversation> GetConversations(Wallet wallet)
	{
		return Conversations.Where(c => c.Wallet == wallet).Select(c => c.Conversation);
	}

	public void AddConversationsFromWallet(Wallet wallet)
	{
		// Feed the dummy data.
		Conversations.Add(new ConversationUpdateTrack("myDebugConversation", wallet));
	}

	private Order MyDummyOrder { get; } = new(null, null, null, DateTimeOffset.MinValue, DateTimeOffset.UtcNow, null, null, null, 0, null, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, null, 0, 0, 0, null, null, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
		"||#WASABI#I need a boat to become King of the Pirates||#SIB#Aye Aye Sencho!||", null, null, null, null, null, null);
}
