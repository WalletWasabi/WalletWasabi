using System;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Behaviors
{
	public class NavBarSelectedIndicatorChildBehavior : AttachedToVisualTreeBehavior<Rectangle>
	{
		private readonly CompositeDisposable _disposables = new();

		public static readonly AttachedProperty<Control> NavBarItemParentProperty =
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

		private void OnLoaded()
		{
			if (AssociatedObject is null)
			{
				return;
			}

			var sharedState = NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);

			var parent = GetNavBarItemParent(AssociatedObject)!;

			Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(parent.Classes, "CollectionChanged")
				.Select(x => parent.Classes)
				.Select(x => x.Contains(":selected")
				             && !x.Contains(":pressed")
				             && !x.Contains(":dragging"))
				.DistinctUntilChanged()
				.Where(x => x)
				.ObserveOn(AvaloniaScheduler.Instance)
				.Subscribe(_ => sharedState.AnimateIndicatorAsync(AssociatedObject));

			AssociatedObject.Opacity = 0;

			if (parent.Classes.Contains(":selected"))
			{
				sharedState.SetActive(AssociatedObject);
			}
		}

		protected override void OnDetaching()
		{
			_disposables.Dispose();
			base.OnDetaching();
		}

		protected override void OnAttachedToVisualTree()
		{
			Dispatcher.UIThread.Post(OnLoaded, DispatcherPriority.Loaded);
		}
	}
}