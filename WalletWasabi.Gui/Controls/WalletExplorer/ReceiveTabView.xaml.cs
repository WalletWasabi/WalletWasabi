using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class ReceiveTabView : UserControl
	{
		public ReceiveTabView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
