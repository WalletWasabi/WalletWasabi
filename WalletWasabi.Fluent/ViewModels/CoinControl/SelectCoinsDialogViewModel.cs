using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

[NavigationMetaData(
	Title = "Select Coins",
	Caption = "",
	IconName = "wallet_action_send",
	NavBarPosition = NavBarPosition.None,
	Searchable = false,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class SelectCoinsDialogViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, TransactionInfo info, IEnumerable<SmartCoin> transactionSpentCoins)
	{
		var pocket = walletViewModel.Wallet.GetPockets();

		var nodes = ToNodes(pocket);

		Source = new HierarchicalTreeDataGridSource<TreeNode>(nodes)
		{
			Columns =
			{
				ColumnFactory.ChildrenColumn(),
				ColumnFactory.IndicatorsColumn(),
				ColumnFactory.AmountColumn(),
				ColumnFactory.PrivacyScore(),
				ColumnFactory.PocketColumn()
			}
		};

		Source.SortBy(Source.Columns[4], ListSortDirection.Descending);

		SetupCancel(false, true, false);
		EnableBack = true;
	}

	public HierarchicalTreeDataGridSource<TreeNode> Source { get; }

	private static IEnumerable<TreeNode> ToNodes(IEnumerable<Pocket> pockets)
	{
		return pockets.Select(p => new TreeNode(new PocketCoinAdapter(p), p.Coins.OrderByDescending(x => x.Amount).Select(coin => new TreeNode(new SmartCoinAdapter(coin)))));
	}
}
