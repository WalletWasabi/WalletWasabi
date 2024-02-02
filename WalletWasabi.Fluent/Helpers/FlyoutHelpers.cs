using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Helpers;

public static class FlyoutHelpers
{
	public static void ShowFlyout(Control target, FlyoutBase flyout, IObservable<bool> condition, CompositeDisposable disposable, bool windowActivityRequired = true)
	{
		var window = VisualLocator
			.Track(target, ancestorLevel: 0, ancestorType: typeof(Window))
			.Cast<Window>();

		window
			.Select(window => window is { }
				? Observable.FromEventPattern<PixelPointEventArgs>(
					handler => window.PositionChanged += handler,
					handler => window.PositionChanged -= handler)
				: Observable.Never<EventPattern<PixelPointEventArgs>>())
			.Switch()
			.Subscribe(e => (e.Sender as Window)?.Focus())
			.DisposeWith(disposable);

		if (windowActivityRequired)
		{
			condition = condition.CombineLatest(
				window
					.Select(window => window?.GetObservable(WindowBase.IsActiveProperty) ?? Observable.Return(false))
					.Switch(),
				static (condition, isActive) => condition && isActive);
		}

		condition
			.Subscribe(isOpen =>
			{
				if (isOpen)
				{
					flyout.ShowAt(target);
				}
				else
				{
					flyout.Hide();
				}
			})
			.DisposeWith(disposable);
	}
}
