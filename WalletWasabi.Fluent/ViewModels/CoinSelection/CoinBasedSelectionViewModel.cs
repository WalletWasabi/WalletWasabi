using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public class CoinBasedSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly int _anonScoreTarget;
	private readonly CompositeDisposable _disposables = new();

	public CoinBasedSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges, int anonScoreTarget)
	{
		_anonScoreTarget = anonScoreTarget;
		coinChanges
			.Transform(model => new TreeNode(model))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var nodes)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(nodes).DisposeWith(_disposables);
	}

	public FlatTreeDataGridSource<TreeNode> Source { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private FlatTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> coins)
	{
		var source = new FlatTreeDataGridSource<TreeNode>(coins)
		{
			Columns =
			{
				ColumnFactory.SelectionColumn(model => model.DisposeWith(_disposables)),
				ColumnFactory.IndicatorsColumn(),
				ColumnFactory.AmountColumn(),
				ColumnFactory.AnonymityScor2e(_anonScoreTarget),
				ColumnFactory.LabelsColumnForCoins(),
			}
		};

		source.RowSelection!.SingleSelect = true;

		return source;
	}
}
