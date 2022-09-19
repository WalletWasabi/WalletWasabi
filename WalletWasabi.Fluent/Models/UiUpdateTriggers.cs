using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models;

public class UiUpdateTriggers
{
	private readonly WalletViewModel _walletViewModel;
	private readonly Wallet _wallet;

	public UiUpdateTriggers(WalletViewModel walletViewModel)
	{
		_walletViewModel = walletViewModel;
		_wallet = _walletViewModel.Wallet;
	}

	public IObservable<Unit> WalletRelevantTransactionProcessed =>
		Observable.FromEventPattern(_wallet.TransactionProcessor, nameof(TransactionProcessor.WalletRelevantTransactionProcessed)).ToSignal();

	public IObservable<Unit> PrivacyModeChanged => Services.UiConfig.WhenAnyValue(x => x.PrivacyMode).ToSignal();

	public IObservable<Unit> UsdExchangeRateChanged => _wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).ToSignal();

	public IObservable<Unit> AnonScoreTargetChanged => _walletViewModel.CoinJoinSettings.WhenAnyValue(x => x.AnonScoreTarget).ToSignal().Throttle(TimeSpan.FromMilliseconds(3100));

	public IObservable<Unit> TransactionHistoryUpdateTrigger =>
		WalletRelevantTransactionProcessed
			.Merge(PrivacyModeChanged)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler);

	public IObservable<Unit> BalanceUpdateTrigger =>
		WalletRelevantTransactionProcessed
			.Merge(PrivacyModeChanged)
			.Merge(UsdExchangeRateChanged)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler);

	public IObservable<Unit> PrivacyProgressUpdateTrigger =>
		WalletRelevantTransactionProcessed
			.Merge(PrivacyModeChanged)
			.Merge(AnonScoreTargetChanged)
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler);

}
