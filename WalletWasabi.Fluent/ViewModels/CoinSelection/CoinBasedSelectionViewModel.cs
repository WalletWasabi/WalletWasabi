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

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<TreeNode> _source = new(Enumerable.Empty<TreeNode>());

	public CoinBasedSelectionViewModel(IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges, IEnumerable<CommandViewModel> commands)
	{
		coinChanges
			.Transform(selectableCoin => new TreeNode(selectableCoin))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var treeNodesCollection)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(treeNodesCollection, coinChanges, commands)
			.DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> coins, IObservable<IChangeSet<SelectableCoin, OutPoint>> selectables, IEnumerable<CommandViewModel> commands)
	{
		var selectionColumn = ColumnFactory.SelectionColumn(
			selectables.Cast(x => (ISelectable) x),
			commands,
			_disposables);

		var source = new HierarchicalTreeDataGridSource<TreeNode>(coins)
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
