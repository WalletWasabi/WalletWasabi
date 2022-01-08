using Avalonia.Collections;

namespace WalletWasabi.Fluent.ViewModels.ResponsivePanelDemo;

public partial class ResponsivePanelDemoViewModel : ViewModelBase
{
	[AutoNotify] private double _itemWidth = 330;
	[AutoNotify] private double _itemHeight = 150;
	[AutoNotify] private double _widthSource = 990;
	[AutoNotify] private double _aspectRatio = double.NaN;
	[AutoNotify] private AvaloniaList<int> _columnHints = new() { 1, 2, 3, 4 };
	[AutoNotify] private AvaloniaList<double> _widthTriggers = new() { 0, 660, 990, 1320 };
}
