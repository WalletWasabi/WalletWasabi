using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Xaml.Interactions.Custom;
using DynamicData.Binding;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ListBoxReselectingBehavior : DisposingBehavior<ListBox>
{
	protected override void OnAttached(CompositeDisposable disposables)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		ItemsCollectionChanged()
			.Where(IsSelectionMoving)
			.Select(x => x.EventArgs.NewStartingIndex)
			.Do(SetSelectedIndex)
			.Subscribe()
			.DisposeWith(disposables);
	}

	private IObservable<EventPattern<NotifyCollectionChangedEventArgs>> ItemsCollectionChanged()
	{
		return this
			.WhenAnyValue(x => x.AssociatedObject!.Items)
			.OfType<INotifyCollectionChanged>()
			.Select(x => x.ObserveCollectionChanges())
			.Switch();
	}

	private void SetSelectedIndex(int newIndex)
	{
		AssociatedObject!.SelectedIndex = newIndex;
	}

	private bool IsSelectionMoving(EventPattern<NotifyCollectionChangedEventArgs> x)
	{
		var isMove = x.EventArgs.Action == NotifyCollectionChangedAction.Move;
		var isSelectedMoving = AssociatedObject!.SelectedIndex == x.EventArgs.OldStartingIndex;
		return isMove && isSelectedMoving;
	}
}
