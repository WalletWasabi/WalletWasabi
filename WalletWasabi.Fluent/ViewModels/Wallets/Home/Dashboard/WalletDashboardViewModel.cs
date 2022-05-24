using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home;

public class WalletDashboardViewModel : ActivatableViewModel, IWalletDashboardViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly Wallet _wallet;

	public WalletDashboardViewModel(WalletViewModelBase wallet, IObservable<Unit> balanceChanged)
	{
		_wallet = wallet.Wallet;
		_balanceChanged = balanceChanged;
	}

	public IObservable<string>? BalanceUsd { get; private set; }

	public IObservable<string>? BalanceBtc { get; private set; }

	public IObservable<bool>? HasBalance { get; private set; }

	public IObservable<string>? BtcToUsdExchangeRate { get; private set; }

	public IObservable<double>? PrivacyScore { get; private set; }

	public IObservable<bool>? HasPrivacyScore { get; private set; }

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		var usdExchangeRate = _wallet.Synchronizer
			.WhenAnyValue(x => x.UsdExchangeRate);

		var totalAmount = _balanceChanged
			.Select(_ => _wallet.Coins.TotalAmount());

		var usdBalance = totalAmount
			.CombineLatest(usdExchangeRate, (btc, usd) => btc.ToDecimal(MoneyUnit.BTC) * usd);

		var privateScore = _balanceChanged
			.Select(_ => GetPrivateScore());

		BtcToUsdExchangeRate = usdExchangeRate
			.Select(usd => usd.FormattedFiat("N0") + " USD")
			.ObserveOn(RxApp.MainThreadScheduler);

		BalanceBtc = totalAmount
			.Select(x => x.ToFormattedString() + " BTC")
			.ObserveOn(RxApp.MainThreadScheduler);

		HasBalance = totalAmount
			.Select(x => x > Money.Zero)
			.ObserveOn(RxApp.MainThreadScheduler);

		BalanceUsd = usdBalance
			.Select(GenerateFiatText)
			.ObserveOn(RxApp.MainThreadScheduler);

		PrivacyScore = privateScore
			.ObserveOn(RxApp.MainThreadScheduler);

		HasPrivacyScore = privateScore
			.Select(x => x > 0d)
			.ObserveOn(RxApp.MainThreadScheduler);
	}

	private static string GenerateFiatText(decimal usdAmount)
	{
		var fiatFormat = usdAmount >= 10 ? "N0" : "N2";
		return usdAmount.GenerateFiatText("USD", fiatFormat);
	}

	private double GetPrivateScore()
	{
		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;
		var currentPrivacyScore = _wallet.Coins.Sum(x =>
			x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		var maxPrivacyScore = _wallet.Coins.TotalAmount().Satoshi * (privateThreshold - 1);
		var privacyScore = maxPrivacyScore == 0 ? 1 : (double) currentPrivacyScore / maxPrivacyScore;
		return privacyScore;
	}
}