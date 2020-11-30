using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;

namespace WalletWasabi.Fluent.Views.AddWallet.Create
{
	public class ConfirmRecoveryWordsView : ReactiveUserControl<ConfirmRecoveryWordsViewModel>
	{
		public ConfirmRecoveryWordsView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}