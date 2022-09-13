using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using ISelectable = WalletWasabi.Fluent.Controls.ISelectable;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class LabelBasedCoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string _filter = "";

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<TreeNode> _source = new(new List<TreeNode>());

	public LabelBasedCoinSelectionViewModel(
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> coinChanges,
		IEnumerable<CommandViewModel> commands)
	{
		coinChanges
			.Group(x => PrivacyLevelKey.Get(x.SmartLabel, x.GetPrivacyLevel()))
			.Transform(
				group =>
				{
					var coinGroup = new CoinGroupViewModel(group.Key, group.Cache.Connect()).DisposeWith(_disposables);
					return new TreeNode(coinGroup, coinGroup.Items.Select(x => new TreeNode(x)));
				})
			.Filter(FilterChanged)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var nodes)
			.Subscribe()
			.DisposeWith(_disposables);

		// Workaround for https://github.com/AvaloniaUI/Avalonia/issues/8913
		nodes.WhenAnyPropertyChanged()
			.WhereNotNull()
			.Throttle(TimeSpan.FromMilliseconds(10), RxApp.MainThreadScheduler)
			.Do(x => UpdateSource(x, coinChanges, commands))
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(nodes, coinChanges, commands).DisposeWith(_disposables);
	}

	private IObservable<Func<TreeNode, bool>> FilterChanged =>
		this
			.WhenAnyValue(x => x.Filter)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Select(FilterFunction);

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static Func<TreeNode, bool> FilterFunction(string? text)
	{
		return tn =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}

			if (tn.Value is CoinGroupViewModel cg)
			{
				var containsLabel = cg.Labels.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
				return containsLabel;
			}

			return false;
		};
	}

	private void UpdateSource(
		IEnumerable<TreeNode> collection,
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> changes,
		IEnumerable<CommandViewModel> commands)
	{
		Source.Dispose();
		Source = CreateGridSource(collection, changes, commands);
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(
		IEnumerable<TreeNode> groups,
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> coinChanges,
		IEnumerable<CommandViewModel> commands)
	{
		var selectionColumn = ColumnFactory.SelectionColumn(
			coinChanges.Cast(model => (ISelectable) model),
			commands,
			_disposables);

		var source = new HierarchicalTreeDataGridSource<TreeNode>(groups)
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
}
