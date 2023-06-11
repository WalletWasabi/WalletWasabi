using System.Reactive.Linq;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Onboarding.Pages;

public class ThirdViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText => "Continue";
	public bool ShowNext => true;
}
