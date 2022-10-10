using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Behaviors;

public class ScrollToSelectedItemBehavior<T> : AttachedToVisualTreeBehavior<Avalonia.Controls.TreeDataGrid> where T : class
{
	public ScrollToSelectedItemBehavior(string str)
	{
		ChildrenProperty = typeof(T).GetProperty(str);
	}

	public PropertyInfo ChildrenProperty { get; }

	protected override void OnAttachedToVisualTree(CompositeDisposable disposables)
	{
		if (AssociatedObject is { SelectionInteraction: { } selection, RowSelection: { } rowSelection })
		{
			Observable.FromEventPattern(selection, nameof(selection.SelectionChanged))
				.Select(_ => rowSelection.SelectedItem as T)
				.WhereNotNull()
				.Throttle(TimeSpan.FromMilliseconds(100), Scheduler.CurrentThread)
				.Do(model => AssociatedObject.BringIntoView(model, GetChildren))
				.Subscribe()
				.DisposeWith(disposables);
		}
	}

	private IEnumerable<T> GetChildren(T x)
	{
		return (IEnumerable<T>) ChildrenProperty.GetValue(x);
	}
}
