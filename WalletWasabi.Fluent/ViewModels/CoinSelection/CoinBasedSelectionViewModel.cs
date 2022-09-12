using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using ISelectable = WalletWasabi.Fluent.ViewModels.CoinSelection.Core.ISelectable;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class CoinBasedSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	[AutoNotify(SetterModifier = AccessModifier.Private)]
	private HierarchicalTreeDataGridSource<TreeNode> _source;

	public CoinBasedSelectionViewModel(
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> coinChanges,
		IEnumerable<CommandViewModel> commands)
	{
		coinChanges
			.Transform(model => new TreeNode(model))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var nodes)
			.Subscribe()
			.DisposeWith(_disposables);

		// Workaround for https://github.com/AvaloniaUI/Avalonia/issues/8913
		nodes.WhenAnyPropertyChanged()
			.WhereNotNull()
			.Throttle(TimeSpan.FromMilliseconds(10), RxApp.MainThreadScheduler)
			.Do(nodes => UpdateSource(nodes, coinChanges, commands))
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(nodes, coinChanges, commands).DisposeWith(_disposables);
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private void UpdateSource(
		ReadOnlyObservableCollection<TreeNode> collection,
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> coinChanges,
		IEnumerable<CommandViewModel> commands)
	{
		Source.Dispose();
		Source = CreateGridSource(collection, coinChanges, commands);
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(
		IEnumerable<TreeNode> coins,
		IObservable<IChangeSet<WalletCoinViewModel, OutPoint>> selectables,
		IEnumerable<CommandViewModel> commands)
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
