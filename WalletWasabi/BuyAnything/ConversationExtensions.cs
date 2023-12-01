namespace WalletWasabi.BuyAnything;

public static class ConversationExtensions
{
	public static bool IsCompleted(this Conversation conversation)
	{
		return conversation.Status == ConversationStatus.Finished;
	}

	public static bool IsUpdatable(this Conversation conversation) =>
		conversation.Status is ConversationStatus.WaitingForUpdates or ConversationStatus.Started;
}
