using System.Collections.Generic;
using WalletWasabi.WebClients.ShopWare.Models;

namespace WalletWasabi.BuyAnything;

public enum ConversationStatus
{
	Started,
	Finished,
	Cancelled,
	WaitingForUpdates
};

public record ConversationId(string WalletId, string OrderNumber, LocalCustomer Customer)
{
	public static readonly ConversationId Empty = new(string.Empty, string.Empty, LocalCustomer.Empty);
}
public record Conversation(ConversationId Id)
{
	public List<ChatMessage> Messages { get; set; } = new();
	public ConversationStatus Status { get; set; } = ConversationStatus.Started;
	public object? Metadata { get; set; }

	public Conversation(ConversationId id, List<ChatMessage> messages, ConversationStatus status, object? metadata) : this(id)
	{
		Messages = messages;
		Status = status;
		Metadata = metadata;
	}
}

