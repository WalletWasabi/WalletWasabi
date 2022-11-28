using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;
using WalletWasabi.Fluent.Views.CoinControl.Core.Headers;

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
	public SelectCoinsDialogViewModel(WalletViewModel walletViewModel, IList<SmartCoin> selectedCoins)
	{
		var pockets = walletViewModel.Wallet.GetPockets();
		var items = CreateItems(pockets);
		SetSelectionState(items, selectedCoins);

		var pocketColumn = PocketColumn();
		Source = new HierarchicalTreeDataGridSource<CoinControlItemViewModelBase>(items)
		{
			Columns =
			{
				ChildrenColumn(),
				IndicatorsColumn(),
				AmountColumn(),
				AnonymityScoreColumn(),
				pocketColumn
			}
		};
		Source.SortBy(pocketColumn, ListSortDirection.Descending);
		Source.RowSelection!.SingleSelect = true;

		SetupCancel(false, true, false);
		EnableBack = true;
		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, selectedCoins));
	}

	public HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> Source { get; }

	private static IColumn<CoinControlItemViewModelBase> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<CoinControlItemViewModelBase>(
			SelectionColumn(),
			group => group.Children,
			node => node.Children.Count > 1,
			node => node.IsExpanded);
	}

	private static TemplateColumn<CoinControlItemViewModelBase> SelectionColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>(
				(_, _) => new CheckBox
				{
					IsEnabled = false,
					[!ToggleButton.IsCheckedProperty] = new Binding(nameof(CoinControlItemViewModelBase.IsSelected))
				},
				true),
			GridLength.Auto);
	}

	private static void SetSelectionState(IEnumerable<CoinControlItemViewModelBase> items, IEnumerable<SmartCoin> selectedCoins)
	{
		var flatten = Flatten(items).ToList();
		var coins = flatten.OfType<CoinCoinControlItemViewModel>();
		var coinsToSelect = coins.Select(c => new { c, IsSelected = selectedCoins.Any(other => other.Outpoint == c.Outpoint) });

		foreach (var tuple in coinsToSelect)
		{
			tuple.c.IsSelected = tuple.IsSelected;
		}

		var pockets = flatten.OfType<PocketCoinControlItemViewModel>();

		foreach (var pocket in pockets)
		{
			pocket.IsSelected = GetSelectionState(pocket.Children.Select(x => x.IsSelected).ToList());
		}
	}

	private static bool? GetSelectionState(IList<bool?> childState)
	{
		if (childState.All(x => x == true))
		{
			return true;
		}

		if (childState.Any(x => x == true))
		{
			return null;
		}

		return false;
	}

	private static IEnumerable<CoinControlItemViewModelBase> Flatten(IEnumerable<CoinControlItemViewModelBase> r) => r.SelectMany(x => new[] { x }.Concat(Flatten(x.Children)));

	private static IColumn<CoinControlItemViewModelBase> AmountColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			"Amount",
			node => node.Amount.ToFormattedString(),
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, Money>(x => x.Amount),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, Money>(x => x.Amount)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> IndicatorsColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new IndicatorsCellView(), true),
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int>(GetIndicatorPriority),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int>(GetIndicatorPriority)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> AnonymityScoreColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			new AnonymityScoreHeaderView(),
			node => node is CoinCoinControlItemViewModel coin ? coin.AnonymityScore.ToString() : "",
			GridLength.Auto,
			new TextColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int?>(b => b.AnonymityScore),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int?>(b => b.AnonymityScore)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> PocketColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"Pocket",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new LabelsCellView(), true),
			GridLength.Star,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = SortAscending<CoinControlItemViewModelBase, int>(GetLabelPriority),
				CompareDescending = SortDescending<CoinControlItemViewModelBase, int>(GetLabelPriority)
			});
	}

	private static int GetLabelPriority(CoinControlItemViewModelBase coin)
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

	private static int GetIndicatorPriority(CoinControlItemViewModelBase x)
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

	private static IReadOnlyCollection<CoinControlItemViewModelBase> CreateItems(IEnumerable<Pocket> pockets)
	{
		return pockets
			.Select(pocket => new PocketCoinControlItemViewModel(pocket))
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
