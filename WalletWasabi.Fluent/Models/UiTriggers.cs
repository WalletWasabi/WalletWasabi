using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models;

public class UiTriggers
{
	private readonly WalletViewModel _walletViewModel;
	private readonly Wallet _wallet;

	public UiTriggers(WalletViewModel walletViewModel)
	{
		_walletViewModel = walletViewModel;
		_wallet = _walletViewModel.Wallet;
	}

	/// <summary>
	/// Triggers on subscription and when a transaction to the wallet is processed or a new filter is processed.
	/// </summary>
	public IObservable<Unit> TransactionsUpdateTrigger =>
		Observable
			.FromEventPattern(_wallet.TransactionProcessor, nameof(TransactionProcessor.WalletRelevantTransactionProcessed)).ToSignal()
			.Merge(Observable.FromEventPattern(_wallet, nameof(Wallet.NewFilterProcessed)).ToSignal())
			.Sample(TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.StartWith(Unit.Default);

	/// <summary>
	/// Triggers on subscription and when the USD exchange rate changed.
	/// </summary>
	public IObservable<Unit> UsdExchangeRateChanged => _wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).ObserveOn(RxApp.MainThreadScheduler).ToSignal();

	/// <summary>
	/// Triggers on subscription and when the anon score target changed.
	/// </summary>
	public IObservable<Unit> AnonScoreTargetChanged => _walletViewModel.CoinJoinSettings.WhenAnyValue(x => x.AnonScoreTarget).ObserveOn(RxApp.MainThreadScheduler).ToSignal();

	/// <summary>
	/// Triggers on subscription and when a transaction is processed to the wallet or the USD exchange rate changed.
	/// </summary>
	public IObservable<Unit> BalanceUpdateTrigger => TransactionsUpdateTrigger.Merge(UsdExchangeRateChanged).Skip(1);

	/// <summary>
	/// Triggers on subscription and when a transaction is processed to the wallet or the anon score target changed.
	/// </summary>
	public IObservable<Unit> PrivacyProgressUpdateTrigger => TransactionsUpdateTrigger.Merge(AnonScoreTargetChanged).Skip(1);
}
