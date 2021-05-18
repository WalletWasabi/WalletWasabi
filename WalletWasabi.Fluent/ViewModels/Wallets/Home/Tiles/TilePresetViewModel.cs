namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public partial class TilePresetViewModel : ViewModelBase
	{
		[AutoNotify] private int _column;
		[AutoNotify] private int _row;
		[AutoNotify] private int _columnSpan;
		[AutoNotify] private int _rowSpan;

		public TilePresetViewModel()
		{
		}

		public TilePresetViewModel(int column, int row, int columnSpan, int rowSpan)
		{
			Column = column;
			Row = row;
			ColumnSpan = columnSpan;
			RowSpan = rowSpan;
		}
	}
}