using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.ViewModels.Onboarding;

namespace WalletWasabi.Fluent.Features.Onboarding;

public partial class Wizard : ReactiveObject, IWizard
{
	[AutoNotify] private int _currentPageIndex;

	public Wizard(IList<IWizardPage> pages)
	{
		Pages = pages;

		var canGoNext = this.WhenAnyValue(x => x.CurrentPageIndex, x => x < Pages.Count - 1);
		var canBack = this.WhenAnyValue(x => x.CurrentPageIndex, x => x > 0);

		GoNextCommand = ReactiveCommand.Create(() => CurrentPageIndex++, canGoNext);
		BackCommand = ReactiveCommand.Create(() => CurrentPageIndex--, canBack);

		ActivePage = this.WhenAnyValue(x => x.CurrentPageIndex).Select(i => Pages[i]);
	}

	public IObservable<IWizardPage> ActivePage { get; }

	public IReactiveCommand BackCommand { get; set; }

	public ICommand GoNextCommand { get; set; }

	public IList<IWizardPage> Pages { get; }
}
