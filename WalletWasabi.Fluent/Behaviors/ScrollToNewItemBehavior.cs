using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DynamicData.Binding;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToNewItemBehavior : AttachedToVisualTreeBehavior<ItemsControl>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		var itemCollectionChanges = this.WhenAnyValue(x => x.AssociatedObject, x => x.AssociatedObject!.DataContext, x => x.AssociatedObject!.Items, (associateControl, _, _) => associateControl)
			.WhereNotNull()
			.Select(x => x.Items as INotifyCollectionChanged)
			.WhereNotNull()
			.Select(incc => incc.ObserveCollectionChanges())
			.Switch();

		var newItem = itemCollectionChanges
			.Where(pattern => pattern.EventArgs.Action == NotifyCollectionChangedAction.Add)
			.Select(eventPattern => eventPattern.EventArgs.NewItems?.Cast<object>().FirstOrDefault())
			.WhereNotNull();

		newItem
			.Do(ScrollTo)
			.Subscribe()
			.DisposeWith(disposable);
	}

	private void ScrollTo(object obj)
	{
		// Avalonia doesn't provide any method to scroll to a given item, so we just scroll to end.
		var scrollViewer = AssociatedObject.FindDescendantOfType<ScrollViewer>() ?? AssociatedObject.FindAncestorOfType<ScrollViewer>() ?? throw new InvalidOperationException("We can't find any ScrollViewer we can scroll.");
		scrollViewer.ScrollToEnd();
	}
}
