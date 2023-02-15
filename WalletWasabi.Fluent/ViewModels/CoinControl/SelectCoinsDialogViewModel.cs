using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

[NavigationMetaData(
	Title = "Coin Selection",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly TransactionInfo _transactionInfo;
	private readonly WalletViewModel _walletViewModel;

	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, IList<SmartCoin> selectedCoins, TransactionInfo transactionInfo)
	{
		_walletViewModel = walletViewModel;
		_transactionInfo = transactionInfo;

		CoinSelector = new CoinSelectorViewModel(walletViewModel, selectedCoins);

		var coinsChanged = CoinSelector.WhenAnyValue(x => x.SelectedCoins);

		EnoughSelected = coinsChanged.Select(AreEnoughToCreateTransaction);
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

	private bool AreEnoughToCreateTransaction(IEnumerable<SmartCoin> coins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(_walletViewModel.Wallet.KeyManager, _transactionInfo, _walletViewModel.Wallet.Coins, coins, _walletViewModel.Wallet.Kitchen.SaltSoup(), out _);
	}
}
