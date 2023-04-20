using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.ViewModels;

public class StandaloneActivatableViewModel : ViewModelBase
{
	private CompositeDisposable? _disposables;

	protected virtual void OnActivated(CompositeDisposable disposables)
	{
	}

	public void Activate()
	{
		_disposables ??= new CompositeDisposable();
		OnActivated(_disposables);
	}

	public void Deactivate()
	{
		_disposables?.Dispose();
		OnDeactivated();
	}

	protected virtual void OnDeactivated()
	{
	}
}
