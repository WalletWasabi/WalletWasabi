using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.Views.AddWallet
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
