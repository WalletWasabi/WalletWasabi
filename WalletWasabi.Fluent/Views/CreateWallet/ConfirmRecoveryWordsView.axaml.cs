using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CreateWallet;

namespace WalletWasabi.Fluent.Views.CreateWallet
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