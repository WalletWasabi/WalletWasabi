using System.Reactive.Disposables;
using System.Windows.Input;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels;

public abstract class TriggerCommandViewModel : RoutableViewModel
{
	public abstract ICommand TargetCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		if (TargetCommand.CanExecute(null))
		{
			TargetCommand.Execute(null);
		}
		Navigate().Back();
		base.OnNavigatedTo(isInHistory, disposables);
	}
}
