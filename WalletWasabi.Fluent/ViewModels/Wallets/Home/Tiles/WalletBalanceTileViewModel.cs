using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(WalletViewModel walletVm)
	{
		var wallet = walletVm.Wallet;

		var balance = walletVm.UiTriggers.BalanceUpdateTrigger
			.Select(_ => wallet.Coins.Available().TotalAmount());

		BalanceBtc = balance
			.Select(money => $"{money.ToFormattedString()} BTC");

		BalanceFiat = balance
			.Select(money => money.BtcToUsd(wallet.Synchronizer.UsdExchangeRate));

		HasBalance = balance
			.Select(money => money > Money.Zero);
	}

	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> BalanceFiat { get; }

	public IObservable<string> BalanceBtc { get; }
}
