using System.Reactive.Disposables;
using Avalonia;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class AttachedToVisualTreeBehavior<T> : DisposingBehavior<T> where T : Visual
{
	private CompositeDisposable? _disposables;

	protected override void OnAttached(CompositeDisposable disposables)
	{
		_disposables = disposables;

		AssociatedObject!.AttachedToVisualTree += AssociatedObjectOnAttachedToVisualTree;
	}

	private void AssociatedObjectOnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
	{
		OnAttachedToVisualTree(_disposables!);
	}

	protected abstract void OnAttachedToVisualTree(CompositeDisposable disposable);

	protected override void OnDetaching()
	{
		base.OnDetaching();

		AssociatedObject!.AttachedToVisualTree -= AssociatedObjectOnAttachedToVisualTree;
	}
}
