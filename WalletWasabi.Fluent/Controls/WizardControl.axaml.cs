using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.Features.Onboarding;

namespace WalletWasabi.Fluent.Controls;
public class WizardControl : TemplatedControl
{
	public static readonly StyledProperty<IWizardViewModel> WizardProperty = AvaloniaProperty.Register<WizardControl, IWizardViewModel>(nameof(WizardProperty));

	public IWizardViewModel Wizard
	{
		get => GetValue(WizardProperty);
		set => SetValue(WizardProperty, value);
	}
}
