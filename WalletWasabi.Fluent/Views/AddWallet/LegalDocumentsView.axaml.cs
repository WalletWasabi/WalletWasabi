using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.Views.AddWallet
{
	public class LegalDocumentsView : ReactiveUserControl<LegalDocumentsViewModel>
	{
		public LegalDocumentsView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
