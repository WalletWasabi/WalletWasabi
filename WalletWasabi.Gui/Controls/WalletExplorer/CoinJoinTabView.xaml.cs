using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabView : UserControl
	{
		public CoinJoinTabView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void TargetButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var panel = ((Button)sender).Parent as Panel;
			if (panel == null) return;
			panel.ContextMenu.Open(panel);
		}
	}
}
