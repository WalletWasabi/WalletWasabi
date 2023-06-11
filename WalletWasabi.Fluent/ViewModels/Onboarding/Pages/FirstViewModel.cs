using System.Reactive.Linq;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Onboarding.Pages;

public class FirstViewModel : ViewModelBase, IWizardPage
{
	public IObservable<bool> IsValid => Observable.Return(true);
	public string NextText => "Get started";
	public bool ShowNext => true;
}
