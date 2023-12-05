using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<OrderUpdateMessage> UpdateTrigger { get; }
	bool HasUnreadMessages(ConversationId id);
	bool IsCompleted(ConversationId id);
	void RemoveOrder(Guid id);
}
