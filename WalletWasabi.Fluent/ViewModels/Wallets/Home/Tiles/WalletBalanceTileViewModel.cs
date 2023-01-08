using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(WalletViewModel walletVm)
	{
		var wallet = walletVm.Wallet;

		var balance = walletVm.UiTriggers.BalanceUpdateTrigger
			.Select(_ => wallet.Coins.TotalAmount());

		BalanceBtc = balance;

		BalanceFiat = balance
			.Select(money => money.BtcToUsd(wallet.Synchronizer.UsdExchangeRate));

		HasBalance = balance
			.Select(money => money > Money.Zero);
	}
	
	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> BalanceFiat { get; }

	public IObservable<Money> BalanceBtc { get; }
}
