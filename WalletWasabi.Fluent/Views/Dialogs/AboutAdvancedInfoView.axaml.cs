using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.Views.Dialogs
{
	public class AboutAdvancedInfoView : ReactiveUserControl<AboutAdvancedInfoViewModel>
	{
		public AboutAdvancedInfoView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}
