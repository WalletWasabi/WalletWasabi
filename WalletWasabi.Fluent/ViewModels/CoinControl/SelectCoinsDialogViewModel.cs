using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NBitcoin;
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
	private readonly WalletViewModel _walletViewModel;
	private readonly TransactionInfo _transactionInfo;

	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, IEnumerable<SmartCoin> selectedCoins, TransactionInfo transactionInfo)
	{
		_walletViewModel = walletViewModel;
		_transactionInfo = transactionInfo;

		CoinSelector = new CoinSelectorViewModel(walletViewModel, selectedCoins);
		
		RequiredAmount = CoinSelector.SelectedCoinsChanged.Select(GetRequiredAmount);
		SelectedAmount = CoinSelector.SelectedCoinsChanged.Select(c => new Money(c.Sum(x => x.Amount)));
		RemainingAmount = SelectedAmount.CombineLatest(RequiredAmount, (selected, remaining) => remaining - selected);
		EnoughSelected = RemainingAmount.Select(remaining => remaining <= Money.Zero);
		
		SetupCancel(false, true, false);
		EnableBack = true;
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, CoinSelector.SelectedCoins), EnoughSelected);
	}

	public CoinSelectorViewModel CoinSelector { get; }

	public IObservable<Money> RemainingAmount { get; }

	public IObservable<Money> SelectedAmount { get; }

	public IObservable<Money> RequiredAmount { get; }

	private Money GetRequiredAmount(IEnumerable<SmartCoin> coins)
	{
		TransactionHelpers.TryBuildTransactionWithoutPrevTx(_walletViewModel.Wallet.KeyManager, _transactionInfo, _walletViewModel.Wallet.Coins, coins, _walletViewModel.Wallet.Kitchen.SaltSoup(), out var minimumAmount);
		return minimumAmount;
	}

	private IObservable<bool> EnoughSelected { get; }

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		CoinSelector.Dispose();

		base.OnNavigatedFrom(isInHistory);
	}
}
