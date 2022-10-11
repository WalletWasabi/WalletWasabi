using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using ISelectable = WalletWasabi.Fluent.Controls.ISelectable;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class LabelBasedCoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly IObservable<string> _filterChanged;

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<TreeNode> _source = new(new List<TreeNode>());

	public LabelBasedCoinSelectionViewModel(IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges, IEnumerable<CommandViewModel> commands, IObservable<string> filterChanged)
	{
		_filterChanged = filterChanged;
		Source = CreateGridSource(coinChanges, commands)
			.DisposeWith(_disposables);
	}

	private IObservable<Func<TreeNode, bool>> FilterChanged => _filterChanged.Select(FilterHelper.FilterFunction<CoinGroupViewModel>);

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IObservable<IChangeSet<SelectableCoin, OutPoint>> coinChanges, IEnumerable<CommandViewModel> commands)
	{
		coinChanges
			.Group(x => PrivacyIndex.Get(x.SmartLabel, x.PrivacyLevel))
			.Transform(ToTreeNode)
			.Filter(FilterChanged)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var treeNodes)
			.Subscribe()
			.DisposeWith(_disposables);

		var selectionColumn = ColumnFactory.SelectionColumn(coinChanges.Cast(model => (ISelectable) model), commands, _disposables);

		var source = new HierarchicalTreeDataGridSource<TreeNode>(treeNodes)
		{
			Columns =
			{
				ColumnFactory.ChildrenColumn(selectionColumn),
				ColumnFactory.IndicatorsColumn(),
				ColumnFactory.AmountColumn(),
				ColumnFactory.AnonymityScore(),
				ColumnFactory.LabelsColumnForGroups()
			}
		};

		source.RowSelection!.SingleSelect = true;
		source.SortBy(source.Columns[4], ListSortDirection.Descending);

		return source;
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
	private TreeNode ToTreeNode(IGroup<SelectableCoin, OutPoint, PrivacyIndex> group)
	{
		var childChanges = group.Cache.Connect();
		var coinGroup = new CoinGroupViewModel(group.Key, childChanges)
			.DisposeWith(_disposables);

		childChanges
			.Transform(x => new TreeNode(x))
			.Bind(out var childNodes)
			.Subscribe()
			.DisposeWith(_disposables);

		return new TreeNode(coinGroup, childNodes);
	}
}
