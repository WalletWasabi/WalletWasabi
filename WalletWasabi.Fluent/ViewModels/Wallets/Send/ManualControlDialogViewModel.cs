using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Transactions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Coins;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(
    Title = "Manual Control",
    IconName = "wallet_action_send",
    NavBarPosition = NavBarPosition.None,
    Searchable = false,
    NavigationTarget = NavigationTarget.DialogScreen)]
public partial class ManualControlDialogViewModel: DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;

	private ManualControlDialogViewModel(IWalletModel walletModel, Wallet wallet)
	{
		CoinList = new CoinListViewModel(walletModel, walletModel.Coins, [], allowCoinjoiningCoinSelection: true, ignorePrivacyMode: true, allowSelection: true);

		EnableBack = true;

		var nextCommandCanExecute =
			CoinList.Selection
					.ToObservableChangeSet()
					.ToCollection()
					.Select(c => c.Count > 0);

		NextCommand = ReactiveCommand.Create(OnNext, nextCommandCanExecute);

		SelectedAmount =
			CoinList.Selection
				    .ToObservableChangeSet()
			        .ToCollection()
			        .Select(c => c.Any() ? walletModel.AmountProvider.Create(c.TotalAmount()) : null);

		SetupCancel(true, true, true);
		_walletModel = walletModel;
		_wallet = wallet;
	}

	public CoinListViewModel CoinList { get; }

	public IObservable<Amount?> SelectedAmount { get; }

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		if (!isInHistory)
		{
			CoinList.Dispose();
		}
	}

	private void OnNext()
	{
		var coins = CoinList.Selection.GetSmartCoins().ToList();

		var sendParameters = new SendFlowModel(_wallet, _walletModel, coins);

		Navigate().To().Send(_walletModel, sendParameters);
	}
}
