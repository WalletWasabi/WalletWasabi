using System.Reactive;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<string> UpdateTrigger { get; }
	bool HasUnreadMessages(string contextToken);
	bool IsCompleted(string contextToken);
	void RemoveOrder(string contextToken);
}
