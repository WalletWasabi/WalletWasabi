using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using WalletWasabi.Fluent.Helpers;

namespace WalletWasabi.Fluent.Behaviors;

public class FocusNextControlAction : AvaloniaObject, IAction
{
	public object Execute(object? sender, object? parameter)
	{
		if (ApplicationHelper.FocusManager is { } focusManager)
		{
			var current = focusManager.GetFocusedElement();
			if (current is not null)
			{
				var options = new FindNextElementOptions() { FocusedElement = current };
				return focusManager.TryMoveFocus(NavigationDirection.Next, options);
			}
		}

		return false;
	}
}
