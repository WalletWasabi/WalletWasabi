using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Tabs.WalletManager.HardwareWallets
{
	public class HardwareWalletView : UserControl
	{
		public HardwareWalletView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
