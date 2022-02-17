using System.Reactive.Disposables;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class ExecuteCommandOnKeyDownBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<Key?> KeyProperty =
		AvaloniaProperty.Register<ButtonExecuteCommandOnKeyDownBehavior, Key?>(nameof(Key));

	public static readonly StyledProperty<bool> IsEnabledProperty =
		AvaloniaProperty.Register<ButtonExecuteCommandOnKeyDownBehavior, bool>(nameof(IsEnabled), true);

	public static readonly StyledProperty<ICommand> CommandProperty =
		AvaloniaProperty.Register<ExecuteCommandOnKeyDownBehavior, ICommand>(nameof(Command));

	public static readonly StyledProperty<object> CommandParameterProperty =
		AvaloniaProperty.Register<ExecuteCommandOnKeyDownBehavior, object>(nameof(CommandParameter));

	public Key? Key
	{
		get => GetValue(KeyProperty);
		set => SetValue(KeyProperty, value);
	}

	public bool IsEnabled
	{
		get => GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
	}

	public ICommand Command
	{
		get => GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public object CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		var control = AssociatedObject;
		if (control is null)
		{
			return;
		}

		if (control.GetVisualRoot() is IInputElement inputRoot)
		{
			inputRoot.AddDisposableHandler(InputElement.KeyDownEvent, RootDefaultKeyDown)
			         .DisposeWith(disposable);
		}
	}

	private void RootDefaultKeyDown(object? sender, KeyEventArgs e)
	{
		var control = AssociatedObject;
		if (control is null)
		{
			return;
		}

		if (Key is { } && e.Key == Key && control.IsVisible && control.IsEnabled && IsEnabled)
		{
			if (!e.Handled && Command?.CanExecute(CommandParameter) == true)
			{
				Command.Execute(CommandParameter);
				e.Handled = true;
			}
		}
	}
}