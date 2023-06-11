using System.Reactive.Linq;
using Avalonia.Controls;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Controls;

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
