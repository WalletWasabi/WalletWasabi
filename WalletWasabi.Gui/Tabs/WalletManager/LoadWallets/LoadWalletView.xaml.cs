using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Gui.Tabs.WalletManager.LoadWallets
{
	internal class LoadWalletView : UserControl
	{
		public LoadWalletView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
