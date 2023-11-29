using System.Reactive;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<Unit> UpdateTrigger { get; }
	bool HasUnreadMessages(Guid id);
	bool IsCompleted(Guid id);
	void RemoveOrder(Guid id);
}
