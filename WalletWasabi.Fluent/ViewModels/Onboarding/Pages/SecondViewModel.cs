using System.Reactive.Linq;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Onboarding.Pages;

public class SecondViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText => "Fees";
	public bool ShowNext => true;
}
