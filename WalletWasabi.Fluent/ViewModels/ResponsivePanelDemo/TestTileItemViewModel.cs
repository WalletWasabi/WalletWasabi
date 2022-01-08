using System.Collections.Generic;
using Avalonia.Media;

namespace WalletWasabi.Fluent.ViewModels.ResponsivePanelDemo;

public partial class TestTileItemViewModel : ViewModelBase
{
	[AutoNotify] private IBrush _background;
	[AutoNotify] private List<int> _columnSpan;
	[AutoNotify] private List<int> _rowSpan;
}
