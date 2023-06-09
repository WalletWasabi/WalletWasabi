using Avalonia;
using Avalonia.Controls.Primitives;
using WalletWasabi.Fluent.Features.Onboarding;

namespace WalletWasabi.Fluent.Controls;
public class WizardControl : TemplatedControl
{
	public static readonly StyledProperty<IWizard> WizardProperty = AvaloniaProperty.Register<WizardControl, IWizard>(nameof(WizardProperty));

	public IWizard Wizard
	{
		get => GetValue(WizardProperty);
		set => SetValue(WizardProperty, value);
	}
}
