using System.Collections.Generic;
using System.Linq;
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
	public ChatMessage[] Messages { get; set; } = [];
}

// Class to keep a track of the last update of a conversation
public class ConversationUpdateTrack(string contextToken, Wallet wallet)
{
	public string ContextToken { get; set; } = contextToken;
	public DateTimeOffset LastUpdate { get; set; }
	public Conversation Conversation { get; set; } = new();
	public Wallet Wallet { get; set; } = wallet;
}

// Class to manage the conversation updates
// This is a toy implementation just to share the idea by code.
public class BuyAnythingManager(TimeSpan period, BuyAnythingClient client) : PeriodicRunner(period)
{
	private BuyAnythingClient Client { get; } = client;
	private List<ConversationUpdateTrack> Conversations { get; } = [];

	public event EventHandler<ConversationUpdateEvent>? ConversationUpdated;

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		foreach (var track in Conversations)
		{
			var orders = track.ContextToken != "myDebugConversation"
				? await Client.GetConversationsUpdateSinceAsync(track.ContextToken, track.LastUpdate, cancel).ConfigureAwait(false)
				: [MyDummyOrder];

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
			return [];
		}

		var messages = customerComment.Split("||", StringSplitOptions.RemoveEmptyEntries);
		if (!messages.Any())
		{
			return [];
		}

		List<ChatMessage> chatMessages = [];

		foreach (var message in messages)
		{
			var items = message.Split("#", StringSplitOptions.RemoveEmptyEntries);

			if (items.Length != 2)
			{
				return [];
			}

			var isMine = items[0] == "WASABI";
			var text = items[1];
			chatMessages.Add(new ChatMessage(isMine, text));
		}

		return chatMessages.ToArray();
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
