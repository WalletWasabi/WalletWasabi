using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcaster;

namespace WalletWasabi.Fluent.Views.TransactionBroadcaster
{
	public class LoadTransactionView : ReactiveUserControl<LoadTransactionViewModel>
	{
		public LoadTransactionView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
