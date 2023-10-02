using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

[NavigationMetaData(
	Title = "Coin Control",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	public SelectCoinsDialogViewModel(IWalletModel wallet, IList<SmartCoin> selectedCoins, TransactionInfo transactionInfo)
	{
		CoinSelector = new CoinSelectorViewModel(wallet, selectedCoins);

		var coinsChanged = CoinSelector.WhenAnyValue(x => x.SelectedCoins);

		EnoughSelected = coinsChanged.Select(c => wallet.Transactions.AreEnoughToCreateTransaction(transactionInfo, c));
		EnableBack = true;
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, CoinSelector.SelectedCoins), EnoughSelected);

		SetupCancel(false, true, false);
	}

	public CoinSelectorViewModel CoinSelector { get; }

	public IObservable<bool> EnoughSelected { get; }

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		CoinSelector.Dispose();

		base.OnNavigatedFrom(isInHistory);
	}
}
