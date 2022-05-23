using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home;

public class WalletDashboardViewModel : ActivatableViewModel
{
	private readonly IObservable<Unit> _balanceChanged;
	private readonly Wallet _wallet;
	private ObservableAsPropertyHelper<string> _balanceBtc = ObservableAsPropertyHelper<string>.Default();

	private ObservableAsPropertyHelper<string> _balancePrivateBtc = ObservableAsPropertyHelper<string>.Default();
	private ObservableAsPropertyHelper<string> _balanceUsd = ObservableAsPropertyHelper<string>.Default();
	private ObservableAsPropertyHelper<string> _btcPrice = ObservableAsPropertyHelper<string>.Default();
	private ObservableAsPropertyHelper<bool> _hasBalance = ObservableAsPropertyHelper<bool>.Default();
	private ObservableAsPropertyHelper<double> _privateScore = ObservableAsPropertyHelper<double>.Default();

	public WalletDashboardViewModel(WalletViewModelBase wallet, IObservable<Unit> balanceChanged)
	{
		_wallet = wallet.Wallet;
		_balanceChanged = balanceChanged;
	}

	public string BalanceUsd => _balanceUsd.Value;
	public string BalanceBtc => _balanceBtc.Value;
	public bool HasBalance => _hasBalance.Value;
	public string BalancePrivateBtc => _balancePrivateBtc.Value;
	public string BtcToUsdExchangeRate => _btcPrice.Value;

	public double PrivacyScore => _privateScore.Value;

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
		
		var privateAmount = _balanceChanged
			.Select(_ => GetPrivateBtcAmount());

		usdExchangeRate
			.Select(usd => usd.FormattedFiat("N0") + " USD")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.BtcToUsdExchangeRate, out _btcPrice)
			.DisposeWith(disposables);

		totalAmount
			.Select(x => x.ToFormattedString() + " BTC")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.BalanceBtc, out _balanceBtc)
			.DisposeWith(disposables);

		totalAmount
			.Select(x => x > Money.Zero)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.HasBalance, out _hasBalance)
			.DisposeWith(disposables);

		usdBalance
			.Select(GenerateFiatText)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.BalanceUsd, out _balanceUsd)
			.DisposeWith(disposables);

		privateScore
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.PrivacyScore, out _privateScore)
			.DisposeWith(disposables);

		privateAmount
			.Select(x => x.ToFormattedString() + " BTC")
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToProperty(this, x => x.BalancePrivateBtc, out _balancePrivateBtc)
			.DisposeWith(disposables);
	}

	private static string GenerateFiatText(decimal usdAmount)
	{
		var fiatFormat =
			usdAmount >= 10
				? "N0"
				: "N2";

		return usdAmount.GenerateFiatText("USD", fiatFormat);
	}

	private Money GetPrivateBtcAmount()
	{
		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;
		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		return privateAmount;
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