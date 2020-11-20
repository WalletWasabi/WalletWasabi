using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.Views.AddWallet
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