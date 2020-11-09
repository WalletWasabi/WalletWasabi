using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CreateWallet;

namespace WalletWasabi.Fluent.Views.CreateWallet
{
	public class RecoveryWordsView : ReactiveUserControl<RecoveryWordsViewModel>
	{
		public RecoveryWordsView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}