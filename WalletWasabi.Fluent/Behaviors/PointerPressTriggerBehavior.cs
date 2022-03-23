using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public class PointerPressTriggerBehavior : Trigger<Control>
{
	private KeyModifiers _savedKeyModifiers = KeyModifiers.None;

	/// <summary>
	/// Identifies the <seealso cref="KeyModifiers"/> avalonia property.
	/// </summary>
	public static readonly StyledProperty<KeyModifiers> KeyModifiersProperty =
		AvaloniaProperty.Register<PointerPressTriggerBehavior, KeyModifiers>(nameof(KeyModifiers));

	/// <summary>
	/// Gets or sets the required key modifiers to execute <see cref="Button.ClickEvent"/> event handler. This is a avalonia property.
	/// </summary>
	public KeyModifiers KeyModifiers
	{
		get => GetValue(KeyModifiersProperty);
		set => SetValue(KeyModifiersProperty, value);
	}

	/// <summary>
	/// Called after the behavior is attached to the <see cref="Behavior.AssociatedObject"/>.
	/// </summary>
	protected override void OnAttached()
	{
		base.OnAttached();

		if (AssociatedObject is { })
		{
			AssociatedObject.AddHandler(InputElement.PointerPressedEvent, PointerPressedHandler, RoutingStrategies.Tunnel);
		}
	}

	/// <summary>
	/// Called when the behavior is being detached from its <see cref="Behavior.AssociatedObject"/>.
	/// </summary>
	protected override void OnDetaching()
	{
		base.OnDetaching();

		if (AssociatedObject is { })
		{
			AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, PointerPressedHandler);
		}
	}

	private void AssociatedObject_OnClick(object? sender, RoutedEventArgs e)
	{
		if (AssociatedObject is { } && KeyModifiers == _savedKeyModifiers)
		{
			Interaction.ExecuteActions(AssociatedObject, Actions, e);
		}
	}

	private void PointerPressedHandler(object? sender, PointerPressedEventArgs e)
	{
		Interaction.ExecuteActions(AssociatedObject, Actions, e);
	}
}