using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Extensions;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.BuyAnything;

// Event that is raised when we detect an update in the server
public record ConversationUpdateEvent(string ConversationId, DateTimeOffset LastUpdate, string NewMessage);

// Class to keep a track of the last update of a conversation
public class ConversationUpdateTrack
{
	public string ContextToken { get; set; }
	public DateTimeOffset LastUpdate { get; set; }
}

// Class to manage the conversation updates
// This is a toy implementation just to share the idea by code.
public class BuyAnythingManager : PeriodicRunner
{
	private BuyAnythingClient Client { get; }
	private List<ConversationUpdateTrack> Conversations { get; } = new();

	public EventHandler<ConversationUpdateEvent> OnConversationUpdate;

	public BuyAnythingManager(TimeSpan period, BuyAnythingClient client) : base(period)
	{
		Client = client;
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		foreach (var track in Conversations)
		{
			var orders = await Client.GetConversationsUpdateSinceAsync(track.ContextToken, track.LastUpdate, cancel).ConfigureAwait(false);
			foreach (var order in orders)
			{
				track.LastUpdate = DateTimeOffset.Now;
				var newMessageFromConcierge = Parse(order.CustomerComment);
				OnConversationUpdate.SafeInvoke(this, new ConversationUpdateEvent (track.ContextToken, order.UpdatedAt!.Value, newMessageFromConcierge));
			}
		}
	}

	private string Parse(string message)
	{
		return message;
	}
}
