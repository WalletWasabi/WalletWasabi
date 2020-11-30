using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcaster;

namespace WalletWasabi.Fluent.Views.TransactionBroadcaster
{
	public class BroadcastTransactionView : ReactiveUserControl<BroadcastTransactionViewModel>
	{
		public BroadcastTransactionView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
