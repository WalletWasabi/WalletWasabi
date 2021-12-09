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

	public bool IsOpen
	{
		get => GetValue(IsOpenProperty);
		set => SetValue(IsOpenProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		Observable
			.FromEventPattern(AssociatedObject, nameof(AssociatedObject.PointerEnter))
			.Subscribe(_ => IsOpen = true)
			.DisposeWith(disposable);

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
			});
	}
}
