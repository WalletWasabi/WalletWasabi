using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.Custom;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnTappedBehavior : DisposingBehavior<Control>
{
	public static readonly StyledProperty<ICommand?> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandOnTappedBehavior, ICommand?>(nameof(Command));

	public static readonly StyledProperty<object?> CommandParameterProperty =
		AvaloniaProperty.Register<ExecuteCommandOnTappedBehavior, object?>(nameof(CommandParameter));

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

	protected override void OnAttached(CompositeDisposable disposables)
	{
		Gestures.TappedEvent.AddClassHandler<InputElement>(
				(x, _) =>
				{
					if (Equals(x, AssociatedObject))
					{
						var parameter = CommandParameter;
						if (Command is { } cmd && cmd.CanExecute(parameter))
						{
							cmd.Execute(parameter);
						}
					}
				},
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble)
			.DisposeWith(disposables);
	}
}
