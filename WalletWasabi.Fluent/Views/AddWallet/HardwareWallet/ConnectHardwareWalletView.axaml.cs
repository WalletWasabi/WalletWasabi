using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

namespace WalletWasabi.Fluent.Views.AddWallet.HardwareWallet
{
	public class ConnectHardwareWalletView : ReactiveUserControl<ConnectHardwareWalletViewModel>
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