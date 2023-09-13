using System.Reactive;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletPrivacyModel
{
	IObservable<int> Progress { get; }

	IObservable<Unit> ProgressUpdated { get; }

	IObservable<bool> IsWalletPrivate { get; }
}
