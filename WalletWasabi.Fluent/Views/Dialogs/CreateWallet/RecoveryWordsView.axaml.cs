using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs.CreateWallet;

namespace WalletWasabi.Fluent.Views.Dialogs.CreateWallet
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
