using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinControl;
using WalletWasabi.Fluent.ViewModels.CoinControl.Core;
using WalletWasabi.Fluent.Views.CoinControl.Core.Cells;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coins;

public static class CoinListDataGridSource
{
	public static HierarchicalTreeDataGridSource<CoinControlItemViewModelBase> Create(IEnumerable<CoinControlItemViewModelBase> source)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView	-			Auto		-				-			true
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			true
		// Selection		SelectionColumnView		-			Auto		-				-			false
		return new HierarchicalTreeDataGridSource<CoinControlItemViewModelBase>(source)
		{
			Columns =
			{
				IndicatorsColumn(),
				AnonymityScoreColumn(),
				AmountColumn(),
				LabelsColumn(),
				SelectionColumn(),
			}
		};
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

	private static IColumn<CoinControlItemViewModelBase> IndicatorsColumn()
	{
		return new HierarchicalExpanderColumn<CoinControlItemViewModelBase>(
			new TemplateColumn<CoinControlItemViewModelBase>(
				null,
				new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new IndicatorsCellView(), true),
				null,
				GridLength.Auto,
				new TemplateColumnOptions<CoinControlItemViewModelBase>
				{
					CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(GetIndicatorPriority),
					CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(GetIndicatorPriority)
				}),
			group => group.Children,
			node => node.HasChildren(),
			node => node.IsExpanded);
	}

	private static TemplateColumn<CoinControlItemViewModelBase> SelectionColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			null,
			new FuncDataTemplate<CoinControlItemViewModelBase>(
				(_, _) => new SelectionCellView(),
				true),
			null,
			GridLength.Auto);
	}

	private static IColumn<CoinControlItemViewModelBase> AmountColumn()
	{
		return new PlainTextColumn<CoinControlItemViewModelBase>(
			null,
			node => $"{node.Amount.ToFormattedString()} BTC",
			GridLength.Auto,
			new ColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(x => x.Amount),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(x => x.Amount)
			});
	}

	private static IColumn<CoinControlItemViewModelBase> AnonymityScoreColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			null,
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new AnonymityScoreCellView(), true),
			null,
			GridLength.Auto,
			new TemplateColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = Sort<CoinControlItemViewModelBase>.Ascending(b => b.AnonymityScore ?? b.Children.Min(x => x.AnonymityScore)),
				CompareDescending = Sort<CoinControlItemViewModelBase>.Descending(b => b.AnonymityScore ?? b.Children.Min(x => x.AnonymityScore))
			});
	}

	private static IColumn<CoinControlItemViewModelBase> LabelsColumn()
	{
		return new TemplateColumn<CoinControlItemViewModelBase>(
			null,
			new FuncDataTemplate<CoinControlItemViewModelBase>((_, _) => new LabelsCellView(), true),
			null,
			GridLength.Star,
			new TemplateColumnOptions<CoinControlItemViewModelBase>
			{
				CompareAscending = CoinControlLabelComparer.Ascending,
				CompareDescending = CoinControlLabelComparer.Descending
			});
	}
}
