using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using Avalonia.VisualTree;

namespace WalletWasabi.Fluent.Behaviors;

public class NavBarSelectedIndicatorChildBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly AttachedProperty<Control> NavBarItemParentProperty =
		AvaloniaProperty.RegisterAttached<NavBarSelectedIndicatorChildBehavior, Control, Control>(
			"NavBarItemParent");

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		Dispatcher.UIThread.Post(() => OnLoaded(disposable), DispatcherPriority.Loaded);
	}

	private void OnLoaded(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		var selectionIndicator = AssociatedObject.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "SelectionIndicator");
		var sharedState = NavBarSelectedIndicatorParentBehavior.GetParentState(AssociatedObject);

		if (sharedState is null || selectionIndicator is null)
		{
			Detach();
			return;
		}

		Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(AssociatedObject.Classes, "CollectionChanged")
			.Select(_ => AssociatedObject.Classes)
			.Select(x => x.Contains(":selected")
			             && !x.Contains(":pressed")
			             && !x.Contains(":dragging"))
			.DistinctUntilChanged()
			.Where(x => x)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => sharedState.AnimateIndicatorAsync(selectionIndicator))
			.DisposeWith(disposable);

		selectionIndicator.Opacity = 0;

		if (AssociatedObject.Classes.Contains(":selected"))
		{
			sharedState.SetActive(selectionIndicator);
		}
	}
}
