using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Views.Wallets.Transactions.Outputs.Cells;
using WalletWasabi.Fluent.Views.Wallets.Transactions.Outputs.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Outputs;

public static class OutputsCoinListDataGridSource
{
	public static HierarchicalTreeDataGridSource<OutputsCoinListItem> Create(IEnumerable<OutputsCoinListItem> source)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// ChangeIndicator	ChangeIndicatorView		<custom>	3 Star		-				-			false
		// Amount			AmountColumnView		Amount		8 Star		-				-			true
		var result = new HierarchicalTreeDataGridSource<OutputsCoinListItem>(source)
		{
			Columns =
			{
				ChangeIndicatorColumn(),
				AmountColumn(),
			}
		};
		return result;
	}

	private static IColumn<OutputsCoinListItem> AmountColumn()
	{
		return new TemplateColumn<OutputsCoinListItem>(
			null,
			new FuncDataTemplate<OutputsCoinListItem>((item, _) => item is { IsChild: true } ? new AmountCellView() : new TextCellView()),
			null,
			new GridLength(8, GridUnitType.Star),
			new TemplateColumnOptions<OutputsCoinListItem>
			{
				CompareAscending = Sort<OutputsCoinListItem>.Ascending(x => x.Amount),
				CompareDescending = Sort<OutputsCoinListItem>.Descending(x => x.Amount)
			});
	}

	private static IColumn<OutputsCoinListItem> ChangeIndicatorColumn()
	{
		return new HierarchicalExpanderColumn<OutputsCoinListItem>(
			new TemplateColumn<OutputsCoinListItem>(
				null,
				new FuncDataTemplate<OutputsCoinListItem>((_, _) => new ChangeIndicatorView(), true),
				null,
				new GridLength(3, GridUnitType.Star)),
			group => group.Children,
			node => node.HasChildren,
			node => node.IsExpanded);
	}
}
