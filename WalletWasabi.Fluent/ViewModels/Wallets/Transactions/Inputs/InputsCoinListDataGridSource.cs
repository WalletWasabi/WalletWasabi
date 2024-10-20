using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Views.Wallets.Transactions.Inputs.Cells;
using WalletWasabi.Fluent.Views.Wallets.Transactions.Inputs.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public static class InputsCoinListDataGridSource
{
	public static HierarchicalTreeDataGridSource<InputsCoinListItem> Create(IEnumerable<InputsCoinListItem> source)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// AnonymityScore	AnonymityColumnView		<custom>	3 Star		-				-			true
		// Amount			AmountColumnView		Amount		8 Star		-				-			true
		var result = new HierarchicalTreeDataGridSource<InputsCoinListItem>(source)
		{
			Columns =
			{
				AnonymityScoreColumn(),
				AmountColumn(),
			}
		};
		return result;
	}

	private static IColumn<InputsCoinListItem> AmountColumn()
	{
		return new TemplateColumn<InputsCoinListItem>(
			null,
			new FuncDataTemplate<InputsCoinListItem>((item, _) => item is { IsChild: true } ? new AmountCellView() : new TextCellView()),
			null,
			new GridLength(8, GridUnitType.Star),
			new TemplateColumnOptions<InputsCoinListItem>
			{
				CompareAscending = Sort<InputsCoinListItem>.Ascending(x => x.Amount),
				CompareDescending = Sort<InputsCoinListItem>.Descending(x => x.Amount)
			});
	}

	private static IColumn<InputsCoinListItem> AnonymityScoreColumn()
	{
		return new HierarchicalExpanderColumn<InputsCoinListItem>(
			new TemplateColumn<InputsCoinListItem>(
				null,
				new FuncDataTemplate<InputsCoinListItem>((_, _) => new AnonymityScoreColumnView(), true),
				null,
				new GridLength(3, GridUnitType.Star),
				new TemplateColumnOptions<InputsCoinListItem>
				{
					CompareAscending = Sort<InputsCoinListItem>.Ascending(b => b.AnonymityScore),
					CompareDescending = Sort<InputsCoinListItem>.Descending(b => b.AnonymityScore)
				}),
			group => group.Children,
			node => true,
			node => node.IsExpanded);
	}
}
