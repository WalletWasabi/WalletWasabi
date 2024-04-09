using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

/// <summary>
/// Focuses the associated or target control when executed.
/// </summary>
public class FocusControlActionCustom : AvaloniaObject, IAction
{
	/// <summary>
	/// Identifies the <seealso cref="TargetControl"/> Avalonia property.
	/// </summary>
	public static readonly StyledProperty<Control?> TargetControlProperty =
		AvaloniaProperty.Register<FocusControlActionCustom, Control?>(nameof(TargetControl));

	/// <summary>
	/// Gets or sets the target control. This is an Avalonia property.
	/// </summary>
	[ResolveByName]
	public Control? TargetControl
	{
		get => GetValue(TargetControlProperty);
		set => SetValue(TargetControlProperty, value);
	}

	/// <summary>
	/// Executes the action.
	/// </summary>
	/// <param name="sender">The <see cref="object"/> that is passed to the action by the behavior. Generally this is <seealso cref="IBehavior.AssociatedObject"/> or a target object.</param>
	/// <param name="parameter">The value of this parameter is determined by the caller.</param>
	/// <returns>Returns null after executed.</returns>
	public virtual object? Execute(object? sender, object? parameter)
	{
		var control = TargetControl ?? sender as Control;
		Dispatcher.UIThread.Post(() => control?.Focus());
		return null;
	}
}
