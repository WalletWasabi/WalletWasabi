using Avalonia;
using Avalonia.Xaml.Interactivity;
using System.Reactive.Disposables;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class DisposingBehavior<T> : Behavior<T> where T : AvaloniaObject
{
	private CompositeDisposable? _disposables;

	protected override void OnAttached()
	{
		base.OnAttached();

		_disposables?.Dispose();

		_disposables = new CompositeDisposable();

		OnAttached(_disposables);
	}

	protected abstract void OnAttached(CompositeDisposable disposables);

	protected override void OnDetaching()
	{
		base.OnDetaching();

		_disposables?.Dispose();
	}
}
