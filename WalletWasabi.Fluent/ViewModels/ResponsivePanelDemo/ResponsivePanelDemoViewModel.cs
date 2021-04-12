using Avalonia.Collections;

namespace WalletWasabi.Fluent.ViewModels.ResponsivePanelDemo
{
	public partial class ResponsivePanelDemoViewModel : ViewModelBase
	{
		[AutoNotify] private double _itemWidth = 228;
		[AutoNotify] private double _itemHeight = 126;
		[AutoNotify] private double _widthSource = double.NaN;
		[AutoNotify] private double _aspectRatio = double.NaN;
		[AutoNotify] private AvaloniaList<int> _columnHints = new() { 1, 2, 3, 4 };
		[AutoNotify] private AvaloniaList<double> _widthTriggers = new() { 228, 456, 684, 912 };
	}
}