using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorChildBehavior : AttachedToVisualTreeBehavior<Rectangle>
{
	public static readonly AttachedProperty<bool>
		IsSelectedProperty =
			AvaloniaProperty.RegisterAttached<NavBarSelectedIndicatorChildBehavior, Rectangle, bool>("IsSelected",
				inherits: true);

	public static bool GetIsSelected(Control element)
	{
		return element.GetValue(IsSelectedProperty);
	}

	public static void SetIsSelected(Control element, bool value)
	{
		element.SetValue(IsSelectedProperty, value);
	}


	public static readonly AttachedProperty<Control>
		NavBarItemParentProperty =
			AvaloniaProperty.RegisterAttached<NavBarSelectedIndicatorChildBehavior, Control, Control>(
				"NavBarItemParent");

	public static Control GetNavBarItemParent(Control element)
	{
		return element.GetValue(NavBarItemParentProperty);
	}

	public static void SetNavBarItemParent(Control element, Control value)
	{
		element.SetValue(NavBarItemParentProperty, value);
	}


	private NavBarSelectedIndicatorState GetSharedState =>
		NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);


	protected override void OnAttachedToVisualTree()
	{
		if (GetSharedState is null)
		{
			Detach();
			return;
		}

		GetSharedState.AddChild(AssociatedObject);

		AssociatedObject.DetachedFromVisualTree += delegate
		{
			GetSharedState.ScopeChildren.TryRemove(AssociatedObject.GetHashCode(), out _);
		};

		var parent = GetNavBarItemParent(AssociatedObject);

		if (parent is null)
		{
			Logger.LogError(
				$"NavBarItem Selection Indicator's parent is null, cannot continue with indicator animations.");
			return;
		}

		Dispatcher.UIThread.Post(() =>
		{
			if (parent.Classes.Contains(":selected"))
			{
				GetSharedState.PreviousIndicator = AssociatedObject;
				GetSharedState.AdornerControl.InitialFix(AssociatedObject);
			}

			AssociatedObject.GetPropertyChangedObservable(IsSelectedProperty)
				.DistinctUntilChanged()
				.Subscribe(x =>
				{
					var parent = GetNavBarItemParent(AssociatedObject);

					if ((bool)x.NewValue &&
					    (GetNavBarItemParent(AssociatedObject)?.Classes.Contains(":selected") ?? false))
					{
						GetSharedState.Animate(AssociatedObject);
					}
				});

			AssociatedObject.Opacity = 0;
		}, DispatcherPriority.Loaded);
	}
}