using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.Views.AddWallet
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