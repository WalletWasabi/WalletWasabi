using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Features.Onboarding;

public partial class WizardViewModel : ReactiveObject, IWizardViewModel
{
	public WizardViewModel(IList<IWizardPage> pages)
	{
		Pages = pages;

		ActivePage = Pages.First();
		GoNextCommand = ReactiveCommand.Create(
			() =>
			{
				CurrentPageIndex++;
				return ActivePage = Pages[CurrentPageIndex];
			},
			this.WhenAnyValue(x => x.CurrentPageIndex, x => x < Pages.Count-1));

		BackCommand = ReactiveCommand.Create(() =>
			{
				CurrentPageIndex--;
				return ActivePage = Pages[CurrentPageIndex];
			},
			this.WhenAnyValue(x => x.CurrentPageIndex, x => x > 0));
	}

	public IReactiveCommand BackCommand { get; set; }

	public ICommand GoNextCommand { get; set; }

	[AutoNotify] private int _currentPageIndex;

	[AutoNotify] private IWizardPage _activePage;

	public IList<IWizardPage> Pages { get; }
}
