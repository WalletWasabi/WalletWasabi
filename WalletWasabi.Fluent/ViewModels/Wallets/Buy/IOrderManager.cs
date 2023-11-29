using System.Reactive;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<ConversationId> UpdateTrigger { get; }
	bool HasUnreadMessages(string id);
	bool IsCompleted(string idS);
	void RemoveOrder(string id);
}
