using System.Collections.Generic;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Onboarding;

public interface IWizard
{
	IObservable<IWizardPage> ActivePage { get; }
	IList<IWizardPage> Pages { get; }
	ICommand GoNextCommand { get; set; }
	IReactiveCommand BackCommand { get; set; }
}
