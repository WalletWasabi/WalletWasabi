using System.Reactive.Linq;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.Features.Onboarding.Pages;

public class SecondViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText => "Fees";
	public bool ShowNext => true;
}
