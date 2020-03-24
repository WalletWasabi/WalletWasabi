using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets
{
	public class ConnectHardwareWalletView : UserControl
	{
		public ConnectHardwareWalletView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
