using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
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
