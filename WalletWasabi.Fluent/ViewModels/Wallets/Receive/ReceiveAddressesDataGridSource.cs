using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using WalletWasabi.Fluent.Views.Wallets.Receive.Columns;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public static class ReceiveAddressesDataGridSource
{
	// [Column]		[View]				[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
	// Actions		ActionsColumnView	-			90			-				-			false
	// Address		AddressColumnView	Address		2*			-				-			true
	// Labels		LabelsColumnView	Labels		210			-				-			false
	public static FlatTreeDataGridSource<AddressViewModel> Create(IEnumerable<AddressViewModel> addresses)
	{
		return new FlatTreeDataGridSource<AddressViewModel>(addresses)
		{
			Columns =
			{
				ActionsColumn(),
				AddressColumn(),
				LabelsColumn()
			}
		};
	}

	private static Comparison<AddressViewModel?> SortDescending<T>(Func<AddressViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return 1;
			}

			if (y is null)
			{
				return -1;
			}

			return Comparer<T>.Default.Compare(selector(y), selector(x));
		};
	}

	private static IColumn<AddressViewModel> ActionsColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			null,
			new FuncDataTemplate<AddressViewModel>((node, ns) => new ActionsColumnView(), true),
			options: new ColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = false
			},
			width: new GridLength(90, GridUnitType.Pixel));
	}

	private static IColumn<AddressViewModel> AddressColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			"Address",
			new FuncDataTemplate<AddressViewModel>((node, ns) => new AddressColumnView(), true),
			options: new ColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = SortAscending(x => x.AddressText),
				CompareDescending = SortDescending(x => x.AddressText)
			},
			width: new GridLength(2, GridUnitType.Star));
	}

	private static IColumn<AddressViewModel> LabelsColumn()
	{
		return new TemplateColumn<AddressViewModel>(
			"Labels",
			new FuncDataTemplate<AddressViewModel>((node, ns) => new LabelsColumnView(), true),
			options: new ColumnOptions<AddressViewModel>
			{
				CanUserResizeColumn = false,
				CanUserSortColumn = true,
				CompareAscending = SortAscending(x => x.Label),
				CompareDescending = SortDescending(x => x.Label)
			},
			width: new GridLength(210, GridUnitType.Pixel));
	}

	private static Comparison<AddressViewModel?> SortAscending<T>(Func<AddressViewModel, T> selector)
	{
		return (x, y) =>
		{
			if (x is null && y is null)
			{
				return 0;
			}

			if (x is null)
			{
				return -1;
			}

			if (y is null)
			{
				return 1;
			}

			return Comparer<T>.Default.Compare(selector(x), selector(y));
		};
	}
}
