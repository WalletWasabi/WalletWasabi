using System;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorParentBehavior : DisposingBehavior<Control>
{
	private static readonly AttachedProperty<NavBarSelectedIndicatorState>
		ParentStateProperty =
			AvaloniaProperty.RegisterAttached<Control, Control, NavBarSelectedIndicatorState>("ParentState",
				inherits: true);

	private static NavBarSelectedIndicatorState GetParentState(Control element)
	{
		return element.GetValue(ParentStateProperty);
	}

	private static void SetParentState(Control element, NavBarSelectedIndicatorState value)
	{
		  element.SetValue(ParentStateProperty, value);
	}

	protected override void OnAttached(CompositeDisposable disposables)
	{
		var k = new NavBarSelectedIndicatorState();
		disposables.Add(k);
		SetParentState(AssociatedObject, k);
	}
}

public class NavBarSelectedIndicatorState : IDisposable
{
	public void Dispose()
	{

	}
}