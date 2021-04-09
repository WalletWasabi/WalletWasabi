using System.Collections.Generic;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views.Wallets.Home.Tiles
{
	public partial class WalletBalanceChartTileViewModel : ViewModelBase
	{
		public List<double> YValues { get; } = new()
		{
			0.25, 0.34, 0.79, 0.98, 1.2, 0.75, 0.68, 1.57
		};

		public List<double> XValues { get; } = new()
		{
			0.25, 0.34, 0.79, 0.98, 1.2, 0.75, 0.68, 1.57
		};
	}
}