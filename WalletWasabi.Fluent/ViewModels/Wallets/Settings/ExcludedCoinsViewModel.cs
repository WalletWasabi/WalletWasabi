using System.Linq;
using System.Reactive;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Settings;

[AppLifetime]
[NavigationMetaData(Title = "Excluded Coins", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ExcludedCoinsViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _wallet;

	public ExcludedCoinsViewModel(IWalletModel wallet)
	{
		_wallet = wallet;
		CoinList = new CoinListViewModel(wallet, wallet.ExcludedCoins.ToList(), true);
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(
			() =>
			{
				ExcludeSelectedCoins();
				Close();
			});
	}

	public CoinListViewModel CoinList { get; set; }

	private void ExcludeSelectedCoins()
	{
		_wallet.ExcludedCoins = CoinList.Selection;
	}
}
