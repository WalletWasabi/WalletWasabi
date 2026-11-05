using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

public partial class WalletPrivacyModel
{
	private readonly Wallet _wallet; // Internal access for complex operations

	public WalletPrivacyModel(IWalletModel walletModel)
	{
		// Internal access to Wallet through WalletModel for model-level operations
		_wallet = ((WalletModel)walletModel).Wallet;

		ProgressUpdated =
			walletModel.Transactions.TransactionProcessed
					   .Merge(walletModel.Settings.WhenAnyValue(x => x.AnonScoreTarget).ToSignal())
					   .ObserveOn(RxApp.MainThreadScheduler)
					   .Skip(1);

		Progress = ProgressUpdated.Select(_ => _wallet.GetPrivacyPercentage());

		IsWalletPrivate = ProgressUpdated.Select(x => _wallet.IsWalletPrivate());
	}

	public IObservable<Unit> ProgressUpdated { get; }

	public IObservable<int> Progress { get; }

	public IObservable<bool> IsWalletPrivate { get; }
}
