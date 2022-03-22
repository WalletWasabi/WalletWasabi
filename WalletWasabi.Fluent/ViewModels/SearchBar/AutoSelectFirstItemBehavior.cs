using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.SearchBarTextPart;

public class AutoSelectFirstItemBehavior : Behavior<SelectingItemsControl>
{
	private IDisposable firstItemSelector;

	protected override void OnAttached()
	{
		base.OnAttached();

		firstItemSelector = AssociatedObject
			.GetObservable(ItemsControl.ItemsProperty)
			.OfType<INotifyCollectionChanged>()
			.Select(FromCollectionChanged)
			.Switch()
			.Throttle(TimeSpan.FromMilliseconds(200))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				SelectFirstItemIfAny();
			});
	}

	private static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> FromCollectionChanged(INotifyCollectionChanged collection)
	{
		return Observable
			.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
				handler => collection.CollectionChanged += handler,
				handler => collection.CollectionChanged -= handler);
	}

	private void SelectFirstItemIfAny()
	{
		var enumerable = AssociatedObject.Items.Cast<object>();
        
		var item = enumerable.FirstOrDefault();
		if (item is null)
		{
			return;
		}

		AssociatedObject.SelectedItem = item;
	}

	protected override void OnDetaching()
	{
		firstItemSelector.Dispose();
		base.OnDetaching();
	}
}