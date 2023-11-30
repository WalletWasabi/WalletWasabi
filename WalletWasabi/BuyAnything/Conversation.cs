namespace WalletWasabi.BuyAnything;

public enum ConversationStatus
{
	Started,
	Finished,
	Cancelled,
	WaitingForUpdates
};

public record ConversationId(string WalletId, string EmailAddress, string Password)
{
	public static readonly ConversationId Empty = new("", "", "");
}
public record Conversation(ConversationId Id, ChatMessage[] Messages, ConversationStatus Status, object Metadata);
