using System.Net;

namespace WalletWasabi.BuyAnything;

public class ConversationUpdateTrack2
{
	public ConversationUpdateTrack2(Conversation2 conversation)
	{
		Conversation = conversation;
	}

	public DateTimeOffset LastUpdate { get; set; }
	public Conversation2 Conversation { get; set; }
	public NetworkCredential Credential => new(Conversation.Id.EmailAddress, Conversation.Id.Password);
}
