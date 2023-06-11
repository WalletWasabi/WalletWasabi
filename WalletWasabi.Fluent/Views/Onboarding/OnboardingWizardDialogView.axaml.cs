using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Views.Onboarding;

public class OnboardingWizardDialogView : UserControl
{
	public OnboardingWizardDialogView()
	{
		InitializeComponent();
	}

	
	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
