using System.Reactive.Disposables;
using Avalonia;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class AttachedToVisualTreeBehavior<T> : DisposingBehavior<T> where T : Visual
{
	private CompositeDisposable? _disposables;

	protected override void OnAttached(CompositeDisposable disposables)
	{
		_disposables = disposables;
	}

    protected override void OnAttachedToVisualTree()
    {
		OnAttachedToVisualTree(_disposables!);
    }

	protected abstract void OnAttachedToVisualTree(CompositeDisposable disposable);
}
