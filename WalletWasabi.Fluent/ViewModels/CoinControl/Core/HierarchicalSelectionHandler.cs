using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class HierarchicalSelectionHandler : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	private bool _canUpdate = true;

	public HierarchicalSelectionHandler(IHierarchicallySelectable model)
	{
		Model = model;

		model.Selectables
			.AsObservableChangeSet()
			.AutoRefresh(x => x.IsSelected)
			.Where(_ => _canUpdate)
			.ToCollection()
			.Select(GetSelectionState)
			.BindTo(model, x => x.IsSelected)
			.DisposeWith(_disposables);

		this.WhenAnyValue(x => x.Model.IsSelected)
			.WhereNotNull()
			.Do(isSelected => Toggle(isSelected!.Value))
			.Subscribe();
	}

	private void Toggle(bool isSelected)
	{
		_canUpdate = false;

		foreach (var item in Model.Selectables)
		{
			item.IsSelected = isSelected;
		}

		_canUpdate = true;
	}

	private IHierarchicallySelectable Model { get; }

	private static bool? GetSelectionState(IReadOnlyCollection<IHierarchicallySelectable> children)
	{
		var selectionCount = children.Count(x => x.IsSelected is true or null);
        
		bool? selectionState = selectionCount == 0
			? false
			: children.Count == selectionCount
				? true
				: null;
		return selectionState;
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
