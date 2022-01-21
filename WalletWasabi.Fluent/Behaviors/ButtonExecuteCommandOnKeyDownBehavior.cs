using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class ButtonExecuteCommandOnKeyDownBehavior : AttachedToVisualTreeBehavior<Button>
{
	public static readonly StyledProperty<Key?> KeyProperty =
		AvaloniaProperty.Register<ButtonExecuteCommandOnKeyDownBehavior, Key?>(nameof(Key));

	public static readonly StyledProperty<bool> IsEnabledProperty =
		AvaloniaProperty.Register<ButtonExecuteCommandOnKeyDownBehavior, bool>(nameof(IsEnabled));

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

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		var button = AssociatedObject;
		if (button is null)
		{
			return;
		}

		if (button.GetVisualRoot() is IInputElement inputRoot)
		{
			inputRoot.AddHandler(InputElement.KeyDownEvent, RootDefaultKeyDown);

			disposable.Add(Disposable.Create(() => inputRoot.RemoveHandler(InputElement.KeyDownEvent, RootDefaultKeyDown)));
		}
	}

	private void RootDefaultKeyDown(object? sender, KeyEventArgs e)
	{
		var button = AssociatedObject;
		if (button is null)
		{
			return;
		}

		if (Key is { } && e.Key == Key && button.IsVisible && button.IsEnabled && IsEnabled)
		{
			if (!e.Handled && button.Command?.CanExecute(button.CommandParameter) == true)
			{
				button.Command.Execute(button.CommandParameter);
				e.Handled = true;
			}
		}
	}
}
