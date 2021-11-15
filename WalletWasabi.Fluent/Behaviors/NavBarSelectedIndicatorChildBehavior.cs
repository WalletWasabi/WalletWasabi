using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors
{
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
			return element?.GetValue(NavBarItemParentProperty) ?? null;
		}

		public static void SetNavBarItemParent(Control element, Control value)
		{
			element?.SetValue(NavBarItemParentProperty, value);
		}

		protected override void OnAttachedToVisualTree()
		{
			Dispatcher.UIThread.Post(async () =>
			{
				var SharedState = NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);

				if (SharedState is null)
				{
					Detach();
					return;
				}

				SharedState.AddChild(AssociatedObject);

				var parent = GetNavBarItemParent(AssociatedObject);

				if (parent is null)
				{
					Logger.LogError(
						$"NavBarItem Selection Indicator's parent is null, cannot continue with indicator animations.");
					return;
				}

				Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(parent.Classes, "CollectionChanged")
					.Select(x => x.EventArgs.NewItems)
					.Where(x => parent is not null)
					.Select(x => (parent.Classes?.Contains(":selected") ?? false)
					             && !parent.Classes.Contains(":pressed")
					             && !parent.Classes.Contains(":dragging"))
					.DistinctUntilChanged()
					.Where(x => x)
					.ObserveOn(AvaloniaScheduler.Instance)
					.Subscribe(_ => { SharedState.Animate(AssociatedObject); });



				AssociatedObject.Opacity = 0;

				if (parent.Classes.Contains(":selected"))
				{
					SharedState.InitialFix(AssociatedObject);
				}
			}, DispatcherPriority.Loaded);
		}
	}
}