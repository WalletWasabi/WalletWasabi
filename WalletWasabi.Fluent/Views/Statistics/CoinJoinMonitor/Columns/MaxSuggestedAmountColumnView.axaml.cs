using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Statistics.CoinJoinMonitor.Columns;

public partial class MaxSuggestedAmountColumnView : UserControl
{
	public MaxSuggestedAmountColumnView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
