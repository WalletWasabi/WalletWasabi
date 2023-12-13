using System.Net;

namespace WalletWasabi.BuyAnything;

public class ConversationUpdateTrack
{
	public ConversationUpdateTrack(Conversation conversation)
	{
		Conversation = conversation;
	}

	public DateTimeOffset LastUpdate { get; set; }
	public Conversation Conversation { get; set; }
	public NetworkCredential Credential => new(Conversation.Id.EmailAddress, Conversation.Id.Password);
}
