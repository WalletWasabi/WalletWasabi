using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;

namespace WalletWasabi.Fluent.Features.Onboarding;

public class WizardDesign : IWizardViewModel
{
	public IWizardPage ActivePage => (IWizardPage) (Pages.FirstOrDefault() ?? new object());
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
