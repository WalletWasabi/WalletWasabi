using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.TransactionBroadcaster;

namespace WalletWasabi.Fluent.Views.TransactionBroadcaster
{
	public class TransactionBroadcasterView : ReactiveUserControl<TransactionBroadcasterViewModel>
	{
		public TransactionBroadcasterView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
