using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
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

		ExpandNodesWithCoinsCommand = ReactiveCommand.Create(() => Expand(Source.Items));

		var groupChanges = coinChanges
			.Group(x => PrivacyIndex.Get(x.SmartLabel, x.PrivacyLevel))
			.Transform(ToTreeNode)
			.Publish()
			.RefCount();

		groupChanges
			.Throttle(TimeSpan.FromSeconds(0.2), RxApp.MainThreadScheduler)
			.ToCollection()
			.ToSignal()
			.InvokeCommand(ExpandNodesWithCoinsCommand);

		groupChanges
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

		Source = source
			.DisposeWith(_disposables);
	}

	public ReactiveCommand<Unit, Unit> ExpandNodesWithCoinsCommand { get; }

	private void Expand(IEnumerable<TreeNode> sourceItems)
	{
		foreach (var sourceItem in sourceItems)
		{
			Expand(sourceItem);
		}
	}

	private void Expand(TreeNode treeNode)
	{
		if (treeNode.Value is CoinGroupViewModel cg)
		{
			treeNode.IsExpanded = cg.Items.Any(x => x.IsSelected);
		}
	}

	private IObservable<Func<TreeNode, bool>> FilterChanged => _filterChanged.Select(CoinNodeFilterHelper.FilterFunction<CoinGroupViewModel>);

	public void Dispose()
	{
		_disposables.Dispose();
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
	private TreeNode ToTreeNode(IGroup<SelectableCoin, OutPoint, PrivacyIndex> group)
	{
		var childChanges = group.Cache.Connect();
		var coinGroup = new CoinGroupViewModel(group.Key, childChanges)
			.DisposeWith(_disposables);

		childChanges
			.Transform(x => new TreeNode(x))
			.Sort(SortExpressionComparer<TreeNode>.Descending(node => ((SelectableCoin)node.Value).Amount))
			.Bind(out var childNodes)
			.Subscribe()
			.DisposeWith(_disposables);

		return new TreeNode(coinGroup, childNodes);
	}
}
