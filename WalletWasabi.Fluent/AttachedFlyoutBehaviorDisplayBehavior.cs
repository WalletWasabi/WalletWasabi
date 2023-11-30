using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactions.Custom;
using ReactiveUI;

namespace WalletWasabi.Fluent;

public class AttachedFlyoutBehaviorDisplayBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<AttachedFlyoutBehaviorDisplayBehavior, bool>(nameof(IsVisible));

	public bool IsVisible
	{
		get => GetValue(IsVisibleProperty);
		set => SetValue(IsVisibleProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var flyout = FlyoutBase.GetAttachedFlyout(AssociatedObject);

		if (flyout is null)
		{
			return;
		}

		this.WhenAnyValue(x => x.IsVisible).Do(
				shouldShow =>
				{
					if (shouldShow)
					{
						flyout.ShowAt(AssociatedObject);
					}
					else
					{
						flyout.Hide();
					}
				})
			.Subscribe()
			.DisposeWith(disposable);
	}
}
