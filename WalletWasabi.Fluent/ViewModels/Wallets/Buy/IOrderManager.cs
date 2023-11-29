using System.Reactive;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<string> UpdateTrigger { get; }
	bool HasUnreadMessages(string id);
	bool IsCompleted(string idS);
	void RemoveOrder(string id);
}
