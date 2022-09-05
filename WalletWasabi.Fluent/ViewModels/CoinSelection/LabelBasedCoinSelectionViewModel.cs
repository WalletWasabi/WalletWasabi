using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class LabelBasedCoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string _filter = "";

	public LabelBasedCoinSelectionViewModel(
		IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		var filterPredicate = this
			.WhenAnyValue(x => x.Filter)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Select(FilterFunction);

		coinChanges
			.Group(x => x.SmartLabel)
			.TransformWithInlineUpdate(
				group =>
				{
					var coinGroup = new CoinGroupViewModel(group.Key, group.Cache.Connect());
					return new TreeNode(coinGroup, coinGroup.Items.Select(x => new TreeNode(x)));
				})
			.Filter(filterPredicate)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var nodes)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(nodes).DisposeWith(_disposables);
	}

	public HierarchicalTreeDataGridSource<TreeNode> Source { get; }

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

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> groups)
	{
		var selectionColumn = ColumnFactory.SelectionColumn(model => model.DisposeWith(_disposables));

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
		source.SortBy(source.Columns[3], ListSortDirection.Descending);

		return source;
	}
}
