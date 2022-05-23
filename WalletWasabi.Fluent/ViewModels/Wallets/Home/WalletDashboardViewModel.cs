using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;
using System.Linq;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home;

public partial class WalletDashboardViewModel : ActivatableViewModel
{
	private readonly WalletViewModel _walletVm;
	private readonly IObservable<Unit> _balanceChanged;

	private readonly Wallet _wallet;
	[AutoNotify] private string _btcPrice;
	[AutoNotify] private string _percentText = "";
	[AutoNotify] private string _balancePrivateBtc = "";

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private bool _hasBalance;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private string? _balanceBtc;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private string? _balanceFiat;

	[AutoNotify] private bool _fullyMixed;
	[AutoNotify] private bool _hasPrivateBalance;

	public WalletDashboardViewModel(WalletViewModel walletVM, IObservable<Unit> balanceChanged)
	{
		_walletVm = walletVM;
		_wallet = _walletVm.Wallet;
		_balanceChanged = balanceChanged;

		_btcPrice = "";
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		_wallet.Synchronizer.WhenAnyValue(x => x.UsdExchangeRate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(usd => BtcPrice = usd.FormattedFiat("N0") + " USD")
			.DisposeWith(disposables);

		_balanceChanged
			.Do(_ => UpdatePrivacyStats())
			.Do(_ => UpdateBalance())
			.Subscribe()
			.DisposeWith(disposables);
	}

	private void UpdatePrivacyStats()
	{
		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;

		var currentPrivacyScore = _wallet.Coins.Sum(x =>
			x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, privateThreshold - 1));
		var maxPrivacyScore = _wallet.Coins.TotalAmount().Satoshi * (privateThreshold - 1);
		var pcPrivate = maxPrivacyScore == 0M ? 100 : (int) (currentPrivacyScore * 100 / maxPrivacyScore);

		PercentText = $"{pcPrivate} %";

		FullyMixed = pcPrivate >= 100;

		var privateAmount = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold).TotalAmount();
		HasPrivateBalance = privateAmount > Money.Zero;
		BalancePrivateBtc = $"{privateAmount.ToFormattedString()} BTC";
	}

	private void UpdateBalance()
	{
		var totalAmount = _wallet.Coins.TotalAmount();

		BalanceBtc = $"{totalAmount.ToFormattedString()} BTC";

		var fiatAmount = _wallet.Coins.TotalAmount().ToDecimal(MoneyUnit.BTC) * _wallet.Synchronizer.UsdExchangeRate;
		var fiatFormat =
			fiatAmount >= 10
				? "N0"
				: "N2";

		BalanceFiat = fiatAmount.GenerateFiatText("USD", fiatFormat);

		var privateThreshold = _wallet.KeyManager.AnonScoreTarget;
		var privateCoins = _wallet.Coins.FilterBy(x => x.HdPubKey.AnonymitySet >= privateThreshold);

		var privateDecimalAmount = privateCoins.TotalAmount();

		HasBalance = totalAmount > Money.Zero;

		BalancePrivateBtc = privateDecimalAmount
			.FormattedBtc() + " BTC";
	}
}