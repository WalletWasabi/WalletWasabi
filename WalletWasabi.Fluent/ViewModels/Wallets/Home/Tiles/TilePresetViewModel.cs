using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class TilePresetViewModel : ViewModelBase
{
	[AutoNotify] private TileSize _tileSize;
	[AutoNotify] private int _column;
	[AutoNotify] private int _row;
	[AutoNotify] private int _columnSpan;
	[AutoNotify] private int _rowSpan;
	[AutoNotify] private bool _isVisible;

	public TilePresetViewModel()
	{
	}

	public TilePresetViewModel(int column, int row, int columnSpan, int rowSpan, TileSize tileSize, bool isVisible = true)
	{
		Column = column;
		Row = row;
		ColumnSpan = columnSpan;
		RowSpan = rowSpan;
		TileSize = tileSize;
		IsVisible = isVisible;
	}
}
