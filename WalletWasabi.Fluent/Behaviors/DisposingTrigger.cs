using System.Reactive.Disposables;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class DisposingTrigger : Trigger
{
	private readonly CompositeDisposable _disposables = new();

	protected override void OnAttached()
	{
		base.OnAttached();

		OnAttached(_disposables);
	}

	protected abstract void OnAttached(CompositeDisposable disposables);

	protected override void OnDetaching()
	{
		base.OnDetaching();

		_disposables.Dispose();
	}
}
