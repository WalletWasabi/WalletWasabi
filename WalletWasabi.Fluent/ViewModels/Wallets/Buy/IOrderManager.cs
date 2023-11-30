using System.Reactive;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<ConversationId> UpdateTrigger { get; }
	bool HasUnreadMessages(ConversationId id);
	bool IsCompleted(ConversationId id);
	void RemoveOrder(ConversationId id);
}
