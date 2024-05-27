using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public abstract class ExecuteCommandBaseBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandBaseBehavior, ICommand?>(nameof(Command));

	public static readonly StyledProperty<object?> CommandParameterProperty =
		AvaloniaProperty.Register<ExecuteCommandBaseBehavior, object?>(nameof(CommandParameter));

	public ICommand? Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public object? CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}
}
