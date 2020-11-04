using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels;
using WalletWasabi.Fluent.ViewModels.RecoverWallet;

namespace WalletWasabi.Fluent.Views.RecoverWallet
{
	public class RecoverWalletView : ReactiveUserControl<RecoverWalletViewModel>
	{
		public RecoverWalletView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
