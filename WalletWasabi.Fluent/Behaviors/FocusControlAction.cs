using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

/// <summary>
/// Focuses the associated or target control when executed.
/// </summary>
public class FocusControlAction : AvaloniaObject, IAction
{
	/// <summary>
	/// Identifies the <seealso cref="TargetControl"/> avalonia property.
	/// </summary>
	public static readonly StyledProperty<Control?> TargetControlProperty =
		AvaloniaProperty.Register<FocusControlAction, Control?>(nameof(TargetControl));

	/// <summary>
	/// Gets or sets the target control. This is a avalonia property.
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
		if (TargetControl is not null)
		{
			TargetControl.Focus();
		}
		else
		{
			if (sender is Control control)
			{
				control.Focus();
			}
		}
		return null;
	}
}
