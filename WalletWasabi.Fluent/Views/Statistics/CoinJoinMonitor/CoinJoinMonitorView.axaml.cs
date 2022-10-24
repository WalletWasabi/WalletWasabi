using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Statistics.CoinJoinMonitor;

public class CoinJoinMonitorView : UserControl
{
	public CoinJoinMonitorView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
