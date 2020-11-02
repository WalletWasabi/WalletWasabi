using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

namespace WalletWasabi.Fluent.AddWallet.CreateWallet
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