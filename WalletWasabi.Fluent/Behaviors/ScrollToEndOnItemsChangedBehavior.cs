using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DynamicData.Binding;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToEndOnItemsChangedBehavior : AttachedToVisualTreeBehavior<ItemsControl>
{
	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		var contextChanges = this.WhenAnyValue(x => x.AssociatedObject, x => x.AssociatedObject!.DataContext, (control, _) => control)
			.Select(control => control!.Items.Cast<object>().LastOrDefault());
			
		var itemCollectionChanges = this.WhenAnyValue(x => x.AssociatedObject, x => x.AssociatedObject!.DataContext, x => x.AssociatedObject!.Items, (associateControl, _, _) => associateControl)
			.WhereNotNull()
			.Select(x => x.Items as INotifyCollectionChanged)
			.WhereNotNull()
			.Select(incc => incc.ObserveCollectionChanges())
			.Switch();

		var newItemFromAdds = itemCollectionChanges
			.Where(pattern => pattern.EventArgs.Action == NotifyCollectionChangedAction.Add)
			.Select(eventPattern => eventPattern.EventArgs.NewItems?.Cast<object>().LastOrDefault());

		var newItemFromResets = itemCollectionChanges
			.Where(pattern => pattern.EventArgs.Action == NotifyCollectionChangedAction.Reset)
			.Select(_ => AssociatedObject?.Items.Cast<object>().LastOrDefault());

		newItemFromAdds
			.Merge(newItemFromResets)
			.Merge(contextChanges)
			.WhereNotNull()
			.Do(ScrollTo)
			.Subscribe()
			.DisposeWith(disposable);
	}

	private void ScrollTo(object obj)
	{
		// Avalonia doesn't provide any method to scroll to a given item, so we just scroll to end.
		var descendant = AssociatedObject.FindDescendantOfType<ScrollViewer>();
		var ancestor = AssociatedObject.FindAncestorOfType<ScrollViewer>();
		var scrollViewer = descendant ?? ancestor ?? throw new InvalidOperationException("We can't find any ScrollViewer we can scroll.");

		Dispatcher.UIThread.Post(scrollViewer.ScrollToEnd, DispatcherPriority.Input);
	}
}
