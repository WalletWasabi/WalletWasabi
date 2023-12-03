using System.Reactive;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public record OrderUpdateMessage(ConversationId Id, string? Command);

public interface IOrderManager
{
	IObservable<OrderUpdateMessage> UpdateTrigger { get; }
	bool HasUnreadMessages(ConversationId id);
	bool IsCompleted(ConversationId id);
	void RemoveOrder(ConversationId id);
}
