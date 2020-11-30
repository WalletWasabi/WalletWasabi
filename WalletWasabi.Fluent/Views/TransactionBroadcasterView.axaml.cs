using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Views
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
