using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

// TODO: Remove when context flyouts are fixed in Avalonia.
public class ContextFlyoutWorkaroundBehavior : DisposingBehavior<Window>
{
	private readonly List<FlyoutBase> _openFlyouts = new();


	protected override IDisposable OnAttachedOverride()
	{
		if (AssociatedObject is null)
		{
			return Disposable.Empty;
		}

		var disposables = new CompositeDisposable();

		FlyoutBase.IsOpenProperty.Changed
			.Subscribe(FlyoutOpenChanged)
			.DisposeWith(disposables);

		AssociatedObject
			.WhenAnyValue(x => x.IsActive, x => x.IsPointerOver,
				(isActive, isPointerOver) => !isActive && !isPointerOver)
			.Where(x => x)
			.Subscribe(_ => CloseFlyouts())
			.DisposeWith(disposables);

		AssociatedObject
			.GetObservable(Visual.BoundsProperty)
			.Subscribe(_ => CloseFlyouts())
			.DisposeWith(disposables);

		Observable
			.FromEventPattern<PixelPointEventArgs>(
				handler =>
				{
					if (AssociatedObject is not null)
					{
						AssociatedObject.PositionChanged += handler;
					}
				},
				handler =>
				{
					if (AssociatedObject is not null)
					{
						AssociatedObject.PositionChanged -= handler;
					}
				})
			.Subscribe(_ => CloseFlyouts())
			.DisposeWith(disposables);

		return disposables;
	}

	protected void CloseFlyouts()
	{
		for (var index = _openFlyouts.Count; index > 0;)
		{
			_openFlyouts[--index].Hide();
		}
	}

	private void FlyoutOpenChanged(AvaloniaPropertyChangedEventArgs<bool> e)
	{
		if (e.Sender is FlyoutBase flyout && flyout.Target is { } target)
		{
			if (e.OldValue.Value)
			{
				_openFlyouts.Remove(flyout);

				return;
			}

			if (target.FindAncestorOfType<Window>() == AssociatedObject)
			{
				_openFlyouts.Add(flyout);
			}
		}
	}
}
