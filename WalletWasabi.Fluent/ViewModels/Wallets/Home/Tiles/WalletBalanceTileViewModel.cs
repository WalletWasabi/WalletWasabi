using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(WalletViewModel walletVm)
	{
		WalletVm = walletVm;

		ExchangeRate = this.WhenAnyValue(x => x.WalletVm.Wallet.Synchronizer.UsdExchangeRate);
		BalanceBtc = this.WhenAnyValue(x => x.WalletVm.UiTriggers.BalanceUpdateTrigger).Select(_ => walletVm.Wallet.Coins.TotalAmount());
		BalanceFiat = BalanceBtc.WithLatestFrom(ExchangeRate, (money, exRate) => money.BtcToUsd(exRate));
		HasBalance = BalanceBtc.Select(money => money > Money.Zero);
	}

	public IObservable<decimal> ExchangeRate { get; }

	private WalletViewModel WalletVm { get; }
	
	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> BalanceFiat { get; }

	public IObservable<Money> BalanceBtc { get; }
}
