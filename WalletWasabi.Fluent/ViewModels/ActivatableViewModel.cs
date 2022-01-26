using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels;

public class ActivatableViewModel : ViewModelBase
{
	protected virtual void OnActivated(CompositeDisposable disposables)
	{
	}

	public void Activate(CompositeDisposable disposables)
	{
		OnActivated(disposables);
	}
}
