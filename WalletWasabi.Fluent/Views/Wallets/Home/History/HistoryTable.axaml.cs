using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace WalletWasabi.Fluent.Views.Wallets.Home.History;

public class CustomTreeDataGridCellsPresenter : TreeDataGridCellsPresenter
{
	protected override Size MeasureOverride(Size availableSize)
	{
		Console.WriteLine($"[CustomTreeDataGridCellsPresenter.MeasureOverride] availableSize='{availableSize}'");
		try
		{
			var result = base.MeasureOverride(availableSize);
			Console.WriteLine($"[CustomTreeDataGridCellsPresenter.MeasureOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		Console.WriteLine($"[CustomTreeDataGridCellsPresenter.ArrangeOverride] finalSize='{finalSize}'");
		try
		{
			var result = base.ArrangeOverride(finalSize);
			Console.WriteLine($"[CustomTreeDataGridCellsPresenter.ArrangeOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}
}

public class CustomTreeDataGrid : Avalonia.Controls.TreeDataGrid, IStyleable
{
	Type IStyleable.StyleKey => typeof(Avalonia.Controls.TreeDataGrid);

	protected override Size MeasureOverride(Size availableSize)
	{
		Console.WriteLine($"[TreeDataGrid.MeasureOverride] availableSize='{availableSize}'");
		try
		{
			var result = base.MeasureOverride(availableSize);
			Console.WriteLine($"[TreeDataGrid.MeasureOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		Console.WriteLine($"[TreeDataGrid.ArrangeOverride] finalSize='{finalSize}'");
		try
		{
			var result = base.ArrangeOverride(finalSize);
			Console.WriteLine($"[TreeDataGrid.ArrangeOverride] result='{result}'");
			return result;
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}
}

public class HistoryTable : UserControl
{
	public HistoryTable()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}