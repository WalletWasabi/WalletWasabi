using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles
{
	public readonly struct DataLegend
	{
		public DataLegend(Money amount, string label, string hexColor, double percentShare)
		{
			Amount = amount;
			Label = label;
			HexColor = hexColor;
			PercentShare = percentShare;
		}

		public Money Amount { get; }
		public string Label { get; }
		public string HexColor { get; }
		public double PercentShare { get; }
	}

	public partial class WalletPieChartTileViewModel : ViewModelBase
	{
		[AutoNotify] private IList<(string color, double percentShare)> _testDataPoints;
		[AutoNotify] private IList<DataLegend> _testDataPointsLegend;

		public WalletPieChartTileViewModel(Wallet wallet)
		{
			TestDataPoints = new List<(string, double)>()
			{
				("#72BD81", 0.8d),
				("#F9DE7D", 0.2d)
			};

			TestDataPointsLegend = new List<DataLegend>
			{
				new (Money.Parse("0.77508"),"Private", "#F9DE7D", 0.2 ),
				new (Money.Parse("3.10032"),"Not Private", "#72BD81", 0.8)
			};
		}
	}
}