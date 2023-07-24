using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;
using WalletWasabi.Fluent.Views.CoinControl.Core.Headers;

namespace WalletWasabi.Fluent.ViewModels.CoinControl;

public static class CoinSelectorDataGridSource
{
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

	private static IColumn<CoinControlItemViewModelBase> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<CoinControlItemViewModelBase>(
			SelectionColumn(),
			group => group.Children,
			node => node.Children.Any(),
			node => node.IsExpanded);
	}

	private static TemplateColumn<CoinControlItemViewModelBase> SelectionColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>(
				(_, _) => new SelectionCellView(),
				true),
			GridLength.Auto);
	}

	private static IColumn<CoinControlItemViewModelBase> AmountColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			"Amount",
			node => node.Amount.ToFormattedString(),
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(x => x.Amount),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(x => x.Amount)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> IndicatorsColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new IndicatorsCellView(), true),
			null,
			GridLength.Auto,
			new TemplateColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(GetIndicatorPriority),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(GetIndicatorPriority)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> AnonymityScoreColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			new AnonymityScoreHeaderView(),
			node => node.AnonymityScore?.ToString() ?? "",
			new GridLength(55, GridUnitType.Pixel),
			new TextColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(b => b.AnonymityScore ?? b.Children.Min(x => x.AnonymityScore)),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(b => b.AnonymityScore ?? b.Children.Min(x => x.AnonymityScore))
			});
	}

	private static IColumn<CoinControlItemViewModelBase> PocketColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			"Pocket",
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new LabelsCellView(), true),
			null,
			GridLength.Star,
			new TemplateColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(GetLabelPriority),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(GetLabelPriority)
			});
	}

	public static HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> Create(IEnumerable<CoinControlItemViewModelBase> source)
	{
		return new HierarchicalTreeDataGridSource<CoinControlItemViewModelBase>(source)
		{
			Columns =
			{
				ChildrenColumn(),
				IndicatorsColumn(),
				AmountColumn(),
				AnonymityScoreColumn(),
				PocketColumn()
			}
		};
	}
}
