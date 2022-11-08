using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Layout;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core.Cells;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core.Headers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using ICoin = WalletWasabi.Fluent.ViewModels.CoinControl.Core.ICoin;

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
	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel)
	{
		var pockets = walletViewModel.Wallet.GetPockets();

		var nodes = ToTreeNodes(pockets);

		Source = new HierarchicalTreeDataGridSource<TreeNode>(nodes)
		{
			Columns =
			{
				ChildrenColumn(),
				IndicatorsColumn(),
				AmountColumn(),
				PrivacyScore(),
				PocketColumn()
			}
		};

		Source.SortBy(Source.Columns[4], ListSortDirection.Descending);
		Source.RowSelection!.SingleSelect = true;

		SetupCancel(false, true, false);
		EnableBack = true;
	}

	public HierarchicalTreeDataGridSource<TreeNode> Source { get; }

	private static HierarchicalExpanderColumn<TreeNode> ChildrenColumn(IColumn<TreeNode>? inner = null)
	{
		inner ??= new TextColumn<TreeNode, string>("", node => "");

		return new HierarchicalExpanderColumn<TreeNode>(
			inner,
			group => group.Children,
			node => node.Children.Count() > 1,
			node => node.IsExpanded);
	}

	private static TemplateColumn<TreeNode> AmountColumn()
	{
		return new TemplateColumn<TreeNode>(
			"Amount",
			new ConstantTemplate<TreeNode>(group => ((ICoin) group.Value).Amount),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = TreeNodeSorting.SortAscending<ICoin, Money>(model => model.Amount),
				CompareDescending = TreeNodeSorting.SortDescending<ICoin, Money>(model => model.Amount)
			});
	}

	private static TemplateColumn<TreeNode> IndicatorsColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(group => new IndicatorsCellViewModel((ICoin) group.Value)),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = TreeNodeSorting.SortAscending<ICoin, int>(GetIndicatorPriority),
				CompareDescending = TreeNodeSorting.SortDescending<ICoin, int>(GetIndicatorPriority)
			});
	}

	private static TemplateColumn<TreeNode> PrivacyScore()
	{
		return new TemplateColumn<TreeNode>(
			new AnonymityScoreHeaderViewModel(),
			new ConstantTemplate<TreeNode>(group => ((ICoin) group.Value).AnonymityScore),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = TreeNodeSorting.SortAscending<ICoin, Money>(model => model.Amount),
				CompareDescending = TreeNodeSorting.SortDescending<ICoin, Money>(model => model.Amount)
			});
	}

	private static TemplateColumn<TreeNode> PocketColumn()
	{
		return new TemplateColumn<TreeNode>(
			"Pocket",
			new ConstantTemplate<TreeNode>(group => new LabelsCellViewModel((ICoin) group.Value), HorizontalAlignment.Left),
			GridLength.Star,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = TreeNodeSorting.SortAscending<ICoin, int>(GetLabelPriority),
				CompareDescending = TreeNodeSorting.SortDescending<ICoin, int>(GetLabelPriority)
			});
	}

	private static int GetLabelPriority(ICoin coin)
	{
		if (coin.Labels == CoinPocketHelper.PrivateFundsText)
		{
			return 3;
		}

		if (coin.Labels == CoinPocketHelper.SemiPrivateFundsText)
		{
			return 2;
		}

		return 1;
	}

	private static int GetIndicatorPriority(ICoin x)
	{
		if (x.IsCoinjoining)
		{
			return 1;
		}

		if (x.BannedUntil.HasValue)
		{
			return 2;
		}

		if (!x.IsConfirmed)
		{
			return 3;
		}

		return 0;
	}

	private static IEnumerable<TreeNode> ToTreeNodes(IEnumerable<Pocket> pockets)
	{
		return pockets.Select(
				pocket => new TreeNode(
					new PocketCoinAdapter(pocket),
					pocket.Coins
						.OrderByDescending(x => x.Amount)
						.Select(coin => new TreeNode(new SmartCoinAdapter(coin))).ToList()))
			.ToList();
	}
}
