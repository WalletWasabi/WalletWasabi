using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Wallets.Home.Tiles.WalletBalanceChart
{
	public class WalletBalanceChartNormalTileView : UserControl
	{
		public WalletBalanceChartNormalTileView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}