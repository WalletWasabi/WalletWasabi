using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public class CoinBasedSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public CoinBasedSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, uint256>> coinChanges)
	{
		coinChanges
			.Transform(model => new TreeNode(model))
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

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> coins)
	{
		var selectionColumn = ColumnFactory.SelectionColumn(model => model.DisposeWith(_disposables));

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
