using NBitcoin;
using WalletWasabi.Wallets;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class WalletBalanceTileViewModel : ViewModelBase
	{
		[AutoNotify] private string _balanceBtc;
		[AutoNotify] private string _balanceFiat;

		public WalletBalanceTileViewModel(Wallet wallet)
		{
			Wallet wallet1 = wallet;

			_balanceBtc = wallet1.Coins.Confirmed().TotalAmount().ToDecimal(MoneyUnit.BTC).FormattedBtc() + " BTC";
			_balanceFiat = wallet1.Coins.Confirmed().TotalAmount().ToDecimal(MoneyUnit.BTC)
				.GenerateFiatText(wallet.Synchronizer.UsdExchangeRate, "USD");
		}
	}
}