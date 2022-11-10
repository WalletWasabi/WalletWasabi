using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core.Headers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;

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
		var items = CreateItems(pockets);

		Source = new HierarchicalTreeDataGridSource<ItemBase>(items)
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

	public HierarchicalTreeDataGridSource<ItemBase> Source { get; }

	private static IColumn<ItemBase> ChildrenColumn(IColumn<ItemBase>? inner = null)
	{
		inner ??= new TextColumn<ItemBase, string>("", node => "");

		return new HierarchicalExpanderColumn<ItemBase>(
			inner,
			group => group.Children,
			node => node.Children.Count > 1,
			node => node.IsExpanded);
	}

	private static IColumn<ItemBase> AmountColumn()
	{
		return new TextColumn<ItemBase, string>(
			"Amount",
			node => node.Amount.ToFormattedString(),
			GridLength.Auto);
	}

	private static IColumn<ItemBase> IndicatorsColumn()
	{
		return new TemplateColumn<ItemBase>(
			"",
			new FuncDataTemplate<ItemBase>((_, _) => new IndicatorsCellView(), true),
			GridLength.Auto,
			new ColumnOptions<ItemBase>
			{
				CompareAscending = SortAscending<ItemBase, int>(GetIndicatorPriority),
				CompareDescending = SortDescending<ItemBase, int>(GetIndicatorPriority)
			});
	}

	private static IColumn<ItemBase> PrivacyScore()
	{
		return new TextColumn<ItemBase, int?>(
			new AnonymityScoreHeaderViewModel(),
			node => node.AnonymityScore,
			GridLength.Auto,
			new TextColumnOptions<ItemBase>
			{
				CompareAscending = SortAscending<ItemBase, int?>(b => b.AnonymityScore),
				CompareDescending = SortDescending<ItemBase, int?>(b => b.AnonymityScore)
			});
	}

	private static IColumn<ItemBase> PocketColumn()
	{
		return new TemplateColumn<ItemBase>(
			"Pocket",
			new FuncDataTemplate<ItemBase>((_, _) => new LabelsCellView(), true),
			GridLength.Star,
			new ColumnOptions<ItemBase>
			{
				CompareAscending = SortAscending<ItemBase, int>(GetLabelPriority),
				CompareDescending = SortDescending<ItemBase, int>(GetLabelPriority)
			});
	}

	private static int GetLabelPriority(ItemBase coin)
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

	private static int GetIndicatorPriority(ItemBase x)
	{
		if (x.IsCoinjoining)
		{
			return 1;
		}

		if (x.BannedUntilUtc.HasValue)
		{
			return 2;
		}

		if (!x.IsConfirmed)
		{
			return 3;
		}

		return 0;
	}

	private static IReadOnlyCollection<ItemBase> CreateItems(IEnumerable<Pocket> pockets)
	{
		return pockets
			.Select(pocket => new PocketItem(pocket))
			.ToList();
	}

	public static Comparison<TSource?> SortAscending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(x!), selector(y!));
	}

	public static Comparison<TSource?> SortDescending<TSource, TProperty>(Func<TSource, TProperty?> selector)
	{
		return (x, y) => Comparer<TProperty>.Default.Compare(selector(y!), selector(x!));
	}
}
