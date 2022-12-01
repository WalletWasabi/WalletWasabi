using System.Reactive;
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
			.Select(_ => wallet.Coins.TotalAmount());

		ExchangeRate = wallet.Synchronizer.UsdExchangeRate;

		BalanceBtc = balance;

		BalanceFiat = balance
			.Select(money => money.BtcToUsd(ExchangeRate));

		HasBalance = balance
			.Select(money => money > Money.Zero);
	}

	public decimal ExchangeRate { get; }

	public IObservable<bool> HasBalance { get; }

	public IObservable<decimal> BalanceFiat { get; }

	public IObservable<Money> BalanceBtc { get; }
}
