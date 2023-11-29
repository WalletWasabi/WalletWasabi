namespace WalletWasabi.BuyAnything;

public class ConversationUpdateTrack
{
	public ConversationUpdateTrack(Conversation conversation)
	{
		Conversation = conversation;
	}

	public DateTimeOffset LastUpdate { get; set; }
	public Conversation Conversation { get; set; }
	public bool IsUpdatable =>
		Conversation.Status == ConversationStatus.WaitingForUpdates ||
		Conversation.Status == ConversationStatus.Started;
}
