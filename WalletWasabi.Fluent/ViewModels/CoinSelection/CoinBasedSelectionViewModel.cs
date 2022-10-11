using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using ISelectable = WalletWasabi.Fluent.Controls.ISelectable;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class CoinBasedSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly IObservable<string> _filterChanged;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<TreeNode> _source = new(Enumerable.Empty<TreeNode>());

	public CoinBasedSelectionViewModel(IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges, IEnumerable<CommandViewModel> commands, IObservable<string> filterChanged)
	{
		_filterChanged = filterChanged;
		Source = CreateGridSource(coinChanges, commands)
			.DisposeWith(_disposables);
	}

	private IObservable<Func<TreeNode, bool>> FilterChanged => _filterChanged.Select(FilterHelper.FilterFunction<SelectableCoin>);

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges, IEnumerable<CommandViewModel> commands)
	{
		coinChanges
			.Transform(selectableCoin => new TreeNode(selectableCoin))
			.Filter(FilterChanged)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var treeNodes)
			.Subscribe()
			.DisposeWith(_disposables);

		var selectionColumn = ColumnFactory.SelectionColumn(coinChanges.Cast(x => (ISelectable) x), commands, _disposables);

		var source = new HierarchicalTreeDataGridSource<TreeNode>(treeNodes)
		{
			Columns =
			{
				ColumnFactory.ChildrenColumn(selectionColumn),
				ColumnFactory.IndicatorsColumn(),
				ColumnFactory.AmountColumn(),
				ColumnFactory.AnonymityScore(),
				ColumnFactory.LabelsColumnForCoins()
			}
		};

		source.RowSelection!.SingleSelect = true;
		source.SortBy(source.Columns[3], ListSortDirection.Descending);

		return source;
	}
}
