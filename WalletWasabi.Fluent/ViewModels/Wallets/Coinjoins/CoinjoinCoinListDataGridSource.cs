using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.Views.Wallets.Coinjoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

public static class CoinjoinCoinListDataGridSource
{
	public static HierarchicalTreeDataGridSource<CoinjoinCoinListItem> Create(IEnumerable<CoinjoinCoinListItem> source)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		var result = new HierarchicalTreeDataGridSource<CoinjoinCoinListItem>(source)
		{
			Columns =
			{
				AnonymityScoreColumn(),
				AmountColumn(),
			}
		};
		return result;
	}

	private static IColumn<CoinjoinCoinListItem> AmountColumn()
	{
		return new PrivacyTextColumn<CoinjoinCoinListItem>(
			null,
			node => node.IsChild ? $"{node.Amount.ToFormattedString()} BTC" : $"{node.Children.Count} out of {node.TotalCoinsOnSideCount} ",
			GridLength.Star,
			new ColumnOptions<CoinjoinCoinListItem>
			{
				CompareAscending = Sort<CoinjoinCoinListItem>.Ascending(x => x.Amount),
				CompareDescending = Sort<CoinjoinCoinListItem>.Descending(x => x.Amount)
			},
			PrivacyCellType.Amount,
			9);
	}

	private static IColumn<CoinjoinCoinListItem> AnonymityScoreColumn()
	{
		return new HierarchicalExpanderColumn<CoinjoinCoinListItem>(
			new TemplateColumn<CoinjoinCoinListItem>(
			null,
			new FuncDataTemplate<CoinjoinCoinListItem>((_, _) => new AnonymityScoreColumnView(), true),
			null,
			GridLength.Star,
			new TemplateColumnOptions<CoinjoinCoinListItem>
			{
				CompareAscending = Sort<CoinjoinCoinListItem>.Ascending(b => b.AnonymityScore),
				CompareDescending = Sort<CoinjoinCoinListItem>.Descending(b => b.AnonymityScore)
			}),
		group => group.Children,
		node => true,
		node => node.IsExpanded);
	}
}
