using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class HistoryTabView : UserControl
	{
		public HistoryTabView()
		{
			InitializeComponent();
			LostFocus += ReceiveTabView_LostFocus;
		}

		private void ReceiveTabView_LostFocus(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var model = DataContext as HistoryTabViewModel;
			model.SelectedTransaction = null;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
