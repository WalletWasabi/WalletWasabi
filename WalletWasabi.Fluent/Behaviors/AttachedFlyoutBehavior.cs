using System.Collections.Generic;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class AttachedFlyoutBehavior<T> : DisposingBehavior<T>
	where T : class, IAvaloniaObject
{
	private readonly List<FlyoutBase> _openFlyouts = new();

	protected override void OnAttached(CompositeDisposable disposables)
	{
		FlyoutBase.IsOpenProperty.Changed
			.Subscribe(FlyoutOpenChanged)
			.DisposeWith(disposables);
	}

	protected IReadOnlyList<FlyoutBase> Flyouts => _openFlyouts;

	protected void CloseFlyouts()
	{
		for (var index = _openFlyouts.Count; index > 0; )
		{
			_openFlyouts[--index].Hide();
		}
	}

	private void FlyoutOpenChanged(AvaloniaPropertyChangedEventArgs<bool> e)
	{
		if (e.Sender is FlyoutBase flyout &&
			flyout.Target is { } target)
		{
			foreach (var ancestor in target.GetVisualAncestors())
			{
				if (ancestor != AssociatedObject)
				{
					continue;
				}

				if (e.NewValue.Value)
				{
					_openFlyouts.Add(flyout);
				}
				else
				{
					_openFlyouts.Remove(flyout);
				}

				break;
			}
		}
	}
}
