using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class BindableFlyoutOpenBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<bool> IsOpenProperty =
		AvaloniaProperty.Register<BindableFlyoutOpenBehavior, bool>(nameof(IsOpen));

	public static readonly StyledProperty<bool> AutoOpenOnPointerOverProperty =
		AvaloniaProperty.Register<BindableFlyoutOpenBehavior, bool>(nameof(AutoOpenOnPointerOver), defaultValue: true);

	public bool IsOpen
	{
		get => GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	public bool AutoOpenOnPointerOver
	{
		get => GetValue(AutoOpenOnPointerOverProperty);
		set => SetValue(AutoOpenOnPointerOverProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		if (AutoOpenOnPointerOver)
		{
			Observable
				.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerEnter))
				.Subscribe(_ => IsOpen = true)
				.DisposeWith(disposable);
		}

		this.WhenAnyValue(x => x.IsOpen)
			.Subscribe(isOpen =>
			{
				if (isOpen)
				{
					FlyoutBase.ShowAttachedFlyout(AssociatedObject);
				}
				else
				{
					FlyoutBase.GetAttachedFlyout(AssociatedObject)?.Hide();
				}
			})
			.DisposeWith(disposable);
	}
}
