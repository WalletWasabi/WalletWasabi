using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

public static class TdgSourceFactory
{
	private static int GetOrderingPriority(WalletCoinViewModel x)
	{
		if (x.Model.IsCoinJoinInProgress)
		{
			return 1;
		}

		if (x.Model.IsBanned)
		{
			return 2;
		}

		if (!x.Model.IsConfirmed)
		{
			return 3;
		}

		return 0;
	}

	public static FlatTreeDataGridSource<WalletCoinViewModel> CreateGridSource(IEnumerable<WalletCoinViewModel> coins)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Indicators		IndicatorsColumnView	-			Auto		-				-			true
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			true
		// Selection		SelectionColumnView		-			Auto		-				-			false
		var source = new FlatTreeDataGridSource<WalletCoinViewModel>(coins)
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

		source.RowSelection!.SingleSelect = true;

		return source;
	}

	private static IColumn<WalletCoinViewModel> SelectionColumn()
	{
		return new TemplateColumn<WalletCoinViewModel>(
			null,
			new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new SelectionColumnView(), true),
			null,
			options: new TemplateColumnOptions<WalletCoinViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = false
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<WalletCoinViewModel> IndicatorsColumn()
	{
		return new TemplateColumn<WalletCoinViewModel>(
			null,
			new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new IndicatorsColumnView(), true),
			null,
			options: new TemplateColumnOptions<WalletCoinViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<WalletCoinViewModel>.Ascending(GetOrderingPriority),
				CompareDescending = Sort<WalletCoinViewModel>.Descending(GetOrderingPriority)
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<WalletCoinViewModel> AmountColumn()
	{
		return new PrivacyTextColumn<WalletCoinViewModel>(
			"Amount",
			node => node.Model.Amount.ToBtcWithUnit(),
			type: PrivacyCellType.Amount,
			options: new ColumnOptions<WalletCoinViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<WalletCoinViewModel>.Ascending(x => x.Model.Amount),
				CompareDescending = Sort<WalletCoinViewModel>.Descending(x => x.Model.Amount),
				MinWidth = new GridLength(145, GridUnitType.Pixel)
			},
			width: new GridLength(0, GridUnitType.Auto),
			numberOfPrivacyChars: 9);
	}

	private static IColumn<WalletCoinViewModel> AnonymityScoreColumn()
	{
		return new TemplateColumn<WalletCoinViewModel>(
			null,
			new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AnonymitySetColumnView(), true),
			null,
			options: new TemplateColumnOptions<WalletCoinViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<WalletCoinViewModel>.Ascending(x => x.Model.AnonScore),
				CompareDescending = Sort<WalletCoinViewModel>.Descending(x => x.Model.AnonScore)
			},
			width: new GridLength(0, GridUnitType.Auto));
	}

	private static IColumn<WalletCoinViewModel> LabelsColumn()
	{
		return new TemplateColumn<WalletCoinViewModel>(
			"Labels",
			new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new LabelsColumnView(), true),
			null,
			options: new TemplateColumnOptions<WalletCoinViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = Sort<WalletCoinViewModel>.Ascending(x => x.Model.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				CompareDescending = Sort<WalletCoinViewModel>.Descending(x => x.Model.Labels, LabelsArrayComparer.OrdinalIgnoreCase),
				MinWidth = new GridLength(100, GridUnitType.Pixel)
			},
			width: new GridLength(1, GridUnitType.Star));
	}
}
