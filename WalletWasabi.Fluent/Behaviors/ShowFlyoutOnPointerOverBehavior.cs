using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowFlyoutOnPointerOverBehavior : AttachedToVisualTreeBehavior<Control>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { } target && FlyoutBase.GetAttachedFlyout(target) is { } flyout)
		{
			var showFlyout = Observable
				.FromEventPattern(target, nameof(AssociatedObject.PointerMoved))
				.Throttle(TimeSpan.FromMicroseconds(100))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Select(_ => target.IsPointerOver);

			FlyoutHelpers.ShowFlyout(target, flyout, showFlyout, disposable, windowActivityRequired: false);
		}
	}
}
