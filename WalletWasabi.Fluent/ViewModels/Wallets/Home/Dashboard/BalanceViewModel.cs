using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Dashboard;

public class BalanceViewModel : ActivatableViewModel
{
	private readonly Wallet _wallet;
	private readonly IObservable<Unit> _balanceChanged;
	public IObservable<string>? BalanceUsd { get; private set; }

	public IObservable<string>? BalanceBtc { get; private set; }

	public IObservable<bool>? HasBalance { get; private set; }

	public IObservable<string>? BtcToUsdExchangeRate { get; private set; }

	public BalanceViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
	{
		_wallet = wallet;
		_balanceChanged = balanceChanged;
	}

	protected override void OnActivated(CompositeDisposable disposables)
	{
		base.OnActivated(disposables);

		var usdExchangeRate = _wallet.Synchronizer
			.WhenAnyValue(x => x.UsdExchangeRate);

		var totalAmount = _balanceChanged
			.Select(_ => _wallet.Coins.TotalAmount());

		var usdBalance = totalAmount
			.CombineLatest(usdExchangeRate, (btc, usd) => btc.ToDecimal(MoneyUnit.BTC) * usd);
		
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
	}

	private static string GenerateFiatText(decimal usdAmount)
	{
		var fiatFormat = usdAmount >= 10 ? "N0" : "N2";
		return usdAmount.GenerateFiatText("USD", fiatFormat);
	}
}