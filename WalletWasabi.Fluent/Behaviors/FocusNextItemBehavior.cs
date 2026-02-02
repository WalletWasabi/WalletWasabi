using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using ReactiveUI;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

internal class FocusNextItemBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<bool> IsFocusedProperty =
		AvaloniaProperty.Register<FocusNextItemBehavior, bool>(nameof(IsFocused), true);

	public bool IsFocused
	{
		get => GetValue(IsFocusedProperty);
		set => SetValue(IsFocusedProperty, value);
	}

	protected override IDisposable OnAttachedOverride()
	{
		return this.WhenAnyValue(x => x.IsFocused)
			.Where(x => x == false)
			.Subscribe(
				_ =>
				{
					var parentControl = AssociatedObject.FindLogicalAncestorOfType<ItemsControl>();

					if (parentControl is { })
					{
						foreach (var item in parentControl.GetLogicalChildren())
						{
							var nextToFocus = item.FindLogicalDescendantOfType<TextBox>();

							if (nextToFocus is not null && nextToFocus.IsEnabled)
							{
								nextToFocus.Focus();
								return;
							}
						}

						parentControl.Focus();
					}
				});
	}
}
