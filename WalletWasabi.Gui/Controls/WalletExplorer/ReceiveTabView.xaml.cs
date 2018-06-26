using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabView : UserControl
	{
		public ReceiveTabView()
		{
			InitializeComponent();
			LostFocus += ReceiveTabView_LostFocus;
		}

		private void ReceiveTabView_LostFocus(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var model = DataContext as ReceiveTabViewModel;
			model.SelectedAddress = null;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
