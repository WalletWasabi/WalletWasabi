using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;

namespace WalletWasabi.Fluent.Controls;

public class MultiSelector : ItemsControl, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Using DisposeWith")]
	public MultiSelector()
    {
        var changes = this
	        .WhenAnyValue(x => x.Items)
	        .WhereNotNull()
	        .OfType<ReadOnlyObservableCollection<ISelectable>>()
	        .Select(x => x.ToObservableChangeSet())
	        .Switch();

		var isSelected = changes
            .AutoRefresh(x => x.IsSelected)
            .ToCollection()
            .Select(GetSelectionState);

        var isSelectedSubject = new BehaviorSubject<bool?>(false);
        isSelected.Subscribe(isSelectedSubject);
	        
        Toggle = ReactiveCommand.CreateFromObservable(
	        () =>
	        {
		        var currentValue = isSelectedSubject.Value;
		        return ToggleChildrenSelection(changes, GetNextValue(currentValue));
	        });

        IsChecked = isSelected
	        .CombineLatest(Toggle.IsExecuting)
	        .Where(x => !x.Second)
	        .Select(x => x.First)
	        .ReplayLastActive();
    }

    public ReactiveCommand<Unit, Unit> Toggle { get; }

    public IObservable<bool?> IsChecked { get; }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _disposables.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private static bool? GetSelectionState(IReadOnlyCollection<ISelectable> collection)
    {
        return collection.All(x => x.IsSelected) ? true : collection.Any(x => x.IsSelected) ? null : false;
    }

    private static IObservable<Unit> ToggleChildrenSelection(IObservable<IChangeSet<ISelectable>> changes, bool getNextValue)
    {
	    return changes
		    .ToCollection()
		    .Do(
			    collection =>
			    {
				    var isChildSelected = getNextValue;
				    collection.ToList().ForEach(notify => notify.IsSelected = isChildSelected);
			    })
		    .Take(1)
		    .ToSignal();
    }

    private static bool GetNextValue(bool? currentValue)
    {
        return !currentValue ?? false;
    }
}
