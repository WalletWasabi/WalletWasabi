using Avalonia.Collections;
using Avalonia.Media;

namespace WalletWasabi.Fluent.ViewModels.ResponsivePanelDemo
{
	public partial class TestTileItemViewModel : ViewModelBase
	{
		[AutoNotify] private IBrush _background;
		[AutoNotify] private AvaloniaList<int> _columnSpan;
		[AutoNotify] private AvaloniaList<int> _rowSpan;
	}
}