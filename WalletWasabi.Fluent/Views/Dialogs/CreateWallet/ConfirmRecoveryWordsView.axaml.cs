using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet;

namespace WalletWasabi.Fluent.Views.Dialogs.CreateWallet
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
