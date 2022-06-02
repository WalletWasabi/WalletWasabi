using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorChildBehavior : AttachedToVisualTreeBehavior<Control>
{
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

	private void OnLoaded(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var sharedState = NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);
		if (sharedState is null)
		{
			Detach();
			return;
		}

		var parent = GetNavBarItemParent(AssociatedObject);

		Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(parent.Classes, "CollectionChanged")
			.Select(_ => parent.Classes)
			.Select(x => x.Contains(":selected")
						 && !x.Contains(":pressed")
						 && !x.Contains(":dragging"))
			.DistinctUntilChanged()
			.Where(x => x)
			.ObserveOn(AvaloniaScheduler.Instance)
			.Subscribe(_ => sharedState.AnimateIndicatorAsync(AssociatedObject))
			.DisposeWith(disposable);

		AssociatedObject.Opacity = 0;

		if (parent.Classes.Contains(":selected"))
		{
			sharedState.SetActive(AssociatedObject);
		}
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		Dispatcher.UIThread.Post(() => OnLoaded(disposable), DispatcherPriority.Loaded);
	}
}
