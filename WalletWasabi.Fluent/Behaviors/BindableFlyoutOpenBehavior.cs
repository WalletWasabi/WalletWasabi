using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class BindableFlyoutOpenBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<bool> IsOpenProperty =
		AvaloniaProperty.Register<BindableFlyoutOpenBehavior, bool>(nameof(IsOpen));

	public bool IsOpen
	{
		get => GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is { } target &&
			FlyoutBase.GetAttachedFlyout(target) is { } flyout)
		{
			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerEntered))
				.Subscribe(_ => IsOpen = true)
				.DisposeWith(disposable);

			FlyoutHelpers.ShowFlyout(target, flyout, this.WhenAnyValue(x => x.IsOpen), disposable);
		}
	}
}
