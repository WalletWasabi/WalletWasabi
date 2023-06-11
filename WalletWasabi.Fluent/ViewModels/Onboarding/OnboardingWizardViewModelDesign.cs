using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;

namespace WalletWasabi.Fluent.ViewModels.Onboarding;

public class WizardDesign : IWizard
{
	public IObservable<IWizardPage> ActivePage => Observable.Return(Pages.First());
	public IList<IWizardPage> Pages { get; }
	public ICommand GoNextCommand { get; set; }
	public IReactiveCommand BackCommand { get; set; }
	public ObservableCollection<IWizardPage> PagesCollection { get; set; } = new();

	public WizardDesign()
	{
		PagesCollection
			.ToObservableChangeSet()
			.Bind(out var pages)
			.Subscribe();

		Pages = pages;
	}
}
