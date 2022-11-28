using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.CoinControl.Core;

public class HierarchicalSelection : ReactiveObject, IDisposable
{
	private bool _canUpdate = true;
	private readonly CompositeDisposable _disposables = new();

	public HierarchicalSelection(IHierarchicallySelectable model)
	{
		var childrenSelectionStates = model.Selectables
			.AsObservableChangeSet()
			.AutoRefresh(x => x.IsSelected, TimeSpan.FromMilliseconds(0.1), scheduler: RxApp.MainThreadScheduler)
			.ToCollection()
			.Select(collection => GetSelectionState(collection.Select(selectable => selectable.IsSelected).ToList()))
			.Where(_ => _canUpdate);

		childrenSelectionStates.BindTo(model, x => x.IsSelected)
			.DisposeWith(_disposables);

		ToggleSelection = ReactiveCommand
			.Create(() => UpdateCheckedStateCascade(model, model.IsSelected ?? false))
			.DisposeWith(_disposables);
	}

	private void UpdateCheckedStateCascade(IHierarchicallySelectable model, bool isChecked)
	{
		_canUpdate = false;

		model.IsSelected = isChecked;

		foreach (var child in model.Selectables)
		{
			UpdateCheckedStateCascade(child, isChecked);
		}

		_canUpdate = true;
	}

	public ReactiveCommand<Unit, Unit> ToggleSelection { get; }

	private static bool? GetSelectionState(IList<bool?> readOnlyCollection)
	{
		bool? selectionState = readOnlyCollection.All(x => x.HasValue && x.Value)
			? true
			: readOnlyCollection.Any(x => x.HasValue && x.Value || !x.HasValue)
				? null
				: false;
		return selectionState;
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
