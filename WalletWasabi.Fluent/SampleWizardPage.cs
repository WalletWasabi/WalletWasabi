using System.Reactive.Linq;
using Avalonia.Controls;
using WalletWasabi.Fluent.Features.Onboarding;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent;

public class SampleWizardPageViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText { get; } = "Next";
	public bool ShowNext => true;
}


public class SampleWizardPageView : UserControl
{
	public SampleWizardPageView()
	{
		Content = new TextBlock { Text = "Sample" };
	}
}
