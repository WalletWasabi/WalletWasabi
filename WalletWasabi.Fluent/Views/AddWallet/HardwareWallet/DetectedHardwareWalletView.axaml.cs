using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet.HardwareWallet;

namespace WalletWasabi.Fluent.Views.AddWallet.HardwareWallet
{
	public class DetectedHardwareWalletView : ReactiveUserControl<DetectedHardwareWalletViewModel>
	{
		public DetectedHardwareWalletView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}