using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public class WalletBalanceTileViewModel : ActivatableViewModel
{
	public WalletBalanceTileViewModel(WalletViewModel walletVm, bool showOnlyAvailable = false)
	{
		var wallet = walletVm.Wallet;

		var trigger = walletVm.UiTriggers.BalanceUpdateTrigger;
		if (showOnlyAvailable)
		{
			trigger = trigger.Merge(walletVm.UiTriggers.WalletCoinsCoinjoinTrigger);
		}

		var balance = trigger.Select(_ => showOnlyAvailable ? wallet.Coins.Available().TotalAmount() : wallet.Coins.TotalAmount());

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
