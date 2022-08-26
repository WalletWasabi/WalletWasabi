using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public class CoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private ReadOnlyObservableCollection<TreeNode> _nodes;

	public CoinSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		coinChanges
			.Transform(model => new TreeNode(model))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out _nodes)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(_nodes).DisposeWith(_disposables);
	}

	public FlatTreeDataGridSource<TreeNode> Source { get; }

	public ReadOnlyObservableCollection<TreeNode> Nodes { get; set; }

	public void Dispose()
	{
		_disposables.Dispose();
	}

	public FlatTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> coins)
	{
		var source = new FlatTreeDataGridSource<TreeNode>(coins)
		{
			Columns =
			{
				ColumnFactory.SelectionColumn(model => model.DisposeWith(_disposables)),
				ColumnFactory.AmountColumn(),
				ColumnFactory.AnonymityScore(),
				ColumnFactory.LabelsColumnForGroups(),
				ColumnFactory.Address()
			}
		};

		source.RowSelection!.SingleSelect = true;

		return source;
	}
}
