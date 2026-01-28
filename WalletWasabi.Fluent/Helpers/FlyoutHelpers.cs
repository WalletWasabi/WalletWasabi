using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using ReactiveUI;

namespace WalletWasabi.Fluent.Helpers;

public static class FlyoutHelpers
{
	public static void ShowFlyout(Control target, FlyoutBase flyout, IObservable<bool> condition, CompositeDisposable disposable, bool windowActivityRequired = true)
	{
		var window = VisualLocator
			.Track(target, ancestorLevel: 0, ancestorType: typeof(Window))
			.WhereNotNull()
			.Cast<Window>();

		window
			.Select(x => Observable.FromEventPattern<PixelPointEventArgs>(
				handler => x.PositionChanged += handler,
				handler => x.PositionChanged -= handler))
			.Switch()
			.Subscribe(e => (e.Sender as Window)?.Focus())
			.DisposeWith(disposable);

		if (windowActivityRequired)
		{
			condition = condition.CombineLatest(
				window
					.Select(x => x.GetObservable(WindowBase.IsActiveProperty))
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
