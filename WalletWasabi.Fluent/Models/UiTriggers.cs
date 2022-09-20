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
	/// Triggers on subscription and when a transaction to the wallet is processed.
	/// </summary>
	public IObservable<Unit> WalletRelevantTransactionProcessed =>
		Observable.FromEventPattern(_wallet.TransactionProcessor, nameof(TransactionProcessor.WalletRelevantTransactionProcessed)).ToSignal().StartWith(Unit.Default);

	/// <summary>
	/// Triggers on subscription and when the USD exchange rate changed.
	/// </summary>
	public IObservable<Unit> UsdExchangeRateChanged => _wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate).ToSignal();

	/// <summary>
	/// Triggers on subscription and when the anon score target changed.
	/// </summary>
	public IObservable<Unit> AnonScoreTargetChanged => _walletViewModel.CoinJoinSettings.WhenAnyValue(x => x.AnonScoreTarget).ToSignal().Throttle(TimeSpan.FromMilliseconds(3100));

	/// <summary>
	/// Triggers on subscription and when a transaction is processed to the wallet or the USD exchange rate changed.
	/// </summary>
	public IObservable<Unit> BalanceUpdateTrigger => WalletRelevantTransactionProcessed.Merge(UsdExchangeRateChanged);

	/// <summary>
	/// Triggers on subscription and when a transaction is processed to the wallet or the anon score target changed.
	/// </summary>
	public IObservable<Unit> PrivacyProgressUpdateTrigger => WalletRelevantTransactionProcessed.Merge(AnonScoreTargetChanged);
}
