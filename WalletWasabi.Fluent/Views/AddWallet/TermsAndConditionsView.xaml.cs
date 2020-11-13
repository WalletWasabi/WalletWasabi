using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.AddWallet;

namespace WalletWasabi.Fluent.Views.AddWallet
{
	public class TermsAndConditionsView : ReactiveUserControl<TermsAndConditionsViewModel>
	{
		public TermsAndConditionsView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
