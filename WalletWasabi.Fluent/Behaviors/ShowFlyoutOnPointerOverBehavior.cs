using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ShowFlyoutOnPointerOverBehavior : DisposingBehavior<Control>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerMoved))
			.Throttle(TimeSpan.FromMilliseconds(100))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => OnPointerMove())
			.DisposeWith(disposables);
	}

	private void OnPointerMove()
	{
		if (AssociatedObject is { } obj && obj.IsPointerOver)
		{
			FlyoutBase.ShowAttachedFlyout(AssociatedObject);
		}
	}
}
