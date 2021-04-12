using System;
using System.Reactive;
using NBitcoin;
using WalletWasabi.Wallets;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceTileViewModel : ViewModelBase
	{
		private readonly Wallet _wallet;
		[AutoNotify] private string _balanceBtc;
		[AutoNotify] private string _balanceFiat;

		public WalletBalanceTileViewModel(Wallet wallet, IObservable<Unit> balanceChanged)
		{
			_wallet = wallet;

			balanceChanged.Subscribe(_ => UpdateBalance());

			UpdateBalance();
		}

		private void UpdateBalance()
		{
			BalanceBtc = _wallet.Coins.Confirmed().TotalAmount().ToDecimal(MoneyUnit.BTC)
				.FormattedBtc() + " BTC";

			BalanceFiat = _wallet.Coins.Confirmed().TotalAmount().ToDecimal(MoneyUnit.BTC)
				.GenerateFiatText(_wallet.Synchronizer.UsdExchangeRate, "USD");
		}
	}
}