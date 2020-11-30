using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;

namespace WalletWasabi.Fluent.Views.AddWallet.Create
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