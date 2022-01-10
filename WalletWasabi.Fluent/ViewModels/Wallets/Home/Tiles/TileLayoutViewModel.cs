namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class TileLayoutViewModel : ViewModelBase
{
	[AutoNotify] private string? _name;
	[AutoNotify] private string? _columnDefinitions;
	[AutoNotify] private string? _rowDefinitions;

	public TileLayoutViewModel()
	{
	}

	public TileLayoutViewModel(string name, string columnDefinitions, string rowDefinitions)
	{
		Name = name;
		ColumnDefinitions = columnDefinitions;
		RowDefinitions = rowDefinitions;
	}
}
