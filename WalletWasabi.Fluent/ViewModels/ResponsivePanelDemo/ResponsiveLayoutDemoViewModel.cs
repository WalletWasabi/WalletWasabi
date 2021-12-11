using System.Collections.ObjectModel;
using Avalonia.Collections;
using Avalonia.Media;

namespace WalletWasabi.Fluent.ViewModels.ResponsivePanelDemo
{
	public partial class ResponsiveLayoutDemoViewModel : ViewModelBase
	{
		[AutoNotify] private double _itemWidth = 330;
		[AutoNotify] private double _itemHeight = 150;
		[AutoNotify] private double _widthSource = 990;
		[AutoNotify] private double _aspectRatio = double.NaN;
		[AutoNotify] private AvaloniaList<int> _columnHints = new() { 1, 2, 3, 4 };
		[AutoNotify] private AvaloniaList<double> _widthTriggers = new() { 0, 660, 990, 1320 };
		[AutoNotify] private ObservableCollection<TestTileItemViewModel> _items;

		public ResponsiveLayoutDemoViewModel()
		{
			_items = new ObservableCollection<TestTileItemViewModel>()
			{
				new()
				{
					Background = Brushes.Red,
					ColumnSpan = new() { 1, 1, 1, 2 },
					RowSpan = new() { 1, 1, 1, 1 }
				},
				new()
				{
					Background = Brushes.Green,
					ColumnSpan = new() { 1, 1, 1, 1 },
					RowSpan = new() { 1, 1, 1, 1 }
				},
				new()
				{
					Background = Brushes.Blue,
					ColumnSpan = new() { 1, 1, 1, 1 },
					RowSpan = new() { 1, 1, 1, 1 }
				},
				new()
				{
					Background = Brushes.Yellow,
					ColumnSpan = new() { 1, 1, 1, 1 },
					RowSpan = new() { 1, 1, 2, 2 }
				},
				new()
				{
					Background = Brushes.Black,
					ColumnSpan = new() { 1, 2, 2, 3 },
					RowSpan = new() { 1, 2, 2, 2 }
				}
			};
		}
	}
}