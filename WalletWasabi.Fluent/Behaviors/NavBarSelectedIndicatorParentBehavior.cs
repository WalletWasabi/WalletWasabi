using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorParentBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly AttachedProperty<NavBarSelectedIndicatorState> ParentStateProperty =
		AvaloniaProperty
			.RegisterAttached<NavBarSelectedIndicatorParentBehavior, Control, NavBarSelectedIndicatorState>(
				"ParentState", inherits: true);

	public static NavBarSelectedIndicatorState? GetParentState(Control element)
	{
		return element.GetValue(ParentStateProperty);
	}

	public static void SetParentState(Control element, NavBarSelectedIndicatorState value)
	{
		element.SetValue(ParentStateProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var sharedState = new NavBarSelectedIndicatorState();
		SetParentState(AssociatedObject, sharedState);
		disposable.Add(sharedState);
	}
}
