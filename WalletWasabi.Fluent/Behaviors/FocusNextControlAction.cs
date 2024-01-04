using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors;

public class FocusNextControlAction : AvaloniaObject, IAction
{
	public object Execute(object? sender, object? parameter)
	{
		// TODO @SuperJMN: Migrate to Avalonia 11
		//if (FocusManager.Instance is { Current: { } current } focusManager)
		//{
		//	var next = KeyboardNavigationHandler.GetNext(current, NavigationDirection.Next);
		//	focusManager.Focus(next);
		//	return true;
		//}

		return false;
	}
}
