using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

internal class FocusBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<bool> IsFocusedProperty =
		AvaloniaProperty.Register<FocusBehavior, bool>(nameof(IsFocused), defaultBindingMode: BindingMode.TwoWay);

	public bool IsFocused
	{
		get => GetValue(IsFocusedProperty);
		set => SetValue(IsFocusedProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		base.OnAttached();

		if (AssociatedObject is not null)
		{
			AssociatedObject.AttachedToLogicalTree += (sender, e) =>
				disposables.Add(this.GetObservable(IsFocusedProperty)
					.Subscribe(focused =>
					{
						if (focused)
						{
							AssociatedObject.Focus();
						}
					}));
		}
	}
}
