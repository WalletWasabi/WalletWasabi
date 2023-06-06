using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Features.Onboarding.Pages;

public class ThirdViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText => "Continue";
	public bool ShowNext => true;
}
