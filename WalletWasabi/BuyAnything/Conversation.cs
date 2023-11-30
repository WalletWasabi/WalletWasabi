namespace WalletWasabi.BuyAnything;

public enum ConversationStatus
{
	Started,
	Finished,
	Cancelled,
	WaitingForUpdates
};

public record ConversationId(string WalletId, string ContextToken)
{
	public static readonly ConversationId Empty = new(string.Empty, string.Empty);
}
public record Conversation(ConversationId Id, ChatMessage[] Messages, ConversationStatus Status, object Metadata);
