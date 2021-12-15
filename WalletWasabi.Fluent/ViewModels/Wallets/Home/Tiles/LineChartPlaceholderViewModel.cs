using System.Collections.ObjectModel;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class LineChartPlaceholderViewModel : ViewModelBase
{
	[AutoNotify] private double? _xMinimum;
	[AutoNotify] private ObservableCollection<double> _yValues;
	[AutoNotify] private ObservableCollection<double> _xValues;

	public LineChartPlaceholderViewModel()
	{
		_xMinimum = 0.0;
		_yValues = new ObservableCollection<double>()
		{
			0, 1, 0, 1
		};
		_xValues = new ObservableCollection<double>()
		{
			0, 1, 2, 3
		};
	}
}