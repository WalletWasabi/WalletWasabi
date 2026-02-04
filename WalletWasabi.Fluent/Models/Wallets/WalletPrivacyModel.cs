using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletPrivacyModel
{
	public WalletPrivacyModel(IWalletModel walletModel, Wallet wallet)
	{
		ProgressUpdated =
			walletModel.Transactions.TransactionProcessed
					   .Merge(walletModel.Settings.WhenAnyValue(x => x.AnonScoreTarget).ToSignal())
					   .ObserveOn(RxApp.MainThreadScheduler)
					   .Skip(1);

		Progress = ProgressUpdated.Select(_ => wallet.GetPrivacyPercentage());

		IsWalletPrivate = ProgressUpdated.Select(x => wallet.IsWalletPrivate());
	}

	public IObservable<Unit> ProgressUpdated { get; }

	public IObservable<int> Progress { get; }

	public IObservable<bool> IsWalletPrivate { get; }
}
