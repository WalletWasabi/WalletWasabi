using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DynamicData.Binding;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ListBoxReselectingBehavior : DisposingBehavior<ListBox>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is not { } listBox)
		{
			return;
		}

		listBox.WhenAnyValue(x => x.Items)
			.Where(x => x is INotifyCollectionChanged)
			.Select(x => x as INotifyCollectionChanged)
			.Do(x => RegisterItemListener(listBox, x, disposables))
			.Subscribe()
			.DisposeWith(disposables);
	}

	private static void RegisterItemListener(SelectingItemsControl listBox,
		INotifyCollectionChanged? walletCollection,
		CompositeDisposable disposables)
	{
		walletCollection?.ObserveCollectionChanges()
			.Where(x => x.EventArgs.Action == NotifyCollectionChangedAction.Move)
			.Do(x =>
			{
				listBox.SelectedIndex = x.EventArgs.NewStartingIndex;
			})
			.Subscribe()
			.DisposeWith(disposables);
	}
}
