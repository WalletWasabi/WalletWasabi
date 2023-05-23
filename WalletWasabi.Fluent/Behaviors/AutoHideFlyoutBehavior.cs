using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class AutoHideFlyoutBehavior : AttachedFlyoutBehavior<Window>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		base.OnAttached(disposables);

		AssociatedObject!
			.WhenAnyValue(x => x.IsActive, x => x.IsPointerOver, (isActive, isPointerOver) => !isActive && !isPointerOver)
			.Where(x => x)
			.Subscribe(_ => CloseFlyouts())
			.DisposeWith(disposables);

		Observable
			.FromEventPattern<PixelPointEventArgs>(
				handler => AssociatedObject!.PositionChanged += handler,
				handler => AssociatedObject!.PositionChanged -= handler)
			.Subscribe(_ => AssociatedObject?.Focus())
			.DisposeWith(disposables);
	}
}
