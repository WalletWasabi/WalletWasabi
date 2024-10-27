using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Views.Wallets.Coinjoins.Cells;
using WalletWasabi.Fluent.Views.Wallets.Coinjoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

public static class CoinjoinCoinListDataGridSource
{
	public static HierarchicalTreeDataGridSource<CoinjoinCoinListItem> Create(IEnumerable<CoinjoinCoinListItem> source)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// AnonymityScore	AnonymityColumnView		<custom>	3 Star		-				-			true
		// Amount			AmountColumnView		Amount		8 Star		-				-			true
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
		return new TemplateColumn<CoinjoinCoinListItem>(
			null,
			new FuncDataTemplate<CoinjoinCoinListItem>((item, _) => item is { IsChild: true } ? new AmountCellView() : new TextCellView()),
			null,
			new GridLength(8, GridUnitType.Star),
			new TemplateColumnOptions<CoinjoinCoinListItem>
			{
				CompareAscending = Sort<CoinjoinCoinListItem>.Ascending(x => x.Amount.Btc),
				CompareDescending = Sort<CoinjoinCoinListItem>.Descending(x => x.Amount.Btc)
			});
	}

	private static IColumn<CoinjoinCoinListItem> AnonymityScoreColumn()
	{
		return new HierarchicalExpanderColumn<CoinjoinCoinListItem>(
			new TemplateColumn<CoinjoinCoinListItem>(
			null,
			new FuncDataTemplate<CoinjoinCoinListItem>((_, _) => new AnonymityScoreColumnView(), true),
			null,
			new GridLength(3, GridUnitType.Star),
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
