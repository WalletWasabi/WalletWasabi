using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.Views.Dialogs
{
	public class AdvancedRecoveryOptionsView : ReactiveUserControl<AdvancedRecoveryOptionsViewModel>
	{
		public AdvancedRecoveryOptionsView()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}
	}
}