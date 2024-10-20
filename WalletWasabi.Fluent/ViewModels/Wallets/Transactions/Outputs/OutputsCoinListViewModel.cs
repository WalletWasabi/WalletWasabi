using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using NBitcoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Outputs;

public class OutputsCoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public OutputsCoinListViewModel(List<TxOut> ownOutputs, List<TxOut> foreignOutputs, bool? isExpanded = null, int? oldOutputCount = null)
	{
		var coinItems = ownOutputs.Union(foreignOutputs).OrderByDescending(x => x.Value).Select(x => new OutputsCoinViewModel(x, ownOutputs.Contains(x))).ToList();
		foreach (var coin in coinItems)
		{
			coin.IsChild = true;
		}

		if (coinItems.Count > 0)
		{
			coinItems.Last().IsLastChild = true;
		}

		var parentItem = new OutputsCoinViewModel(coinItems.ToArray(), ownOutputs.Count + foreignOutputs.Count, isExpanded.GetValueOrDefault(), oldOutputCount);
		coinItems.Insert(0, parentItem);
		_disposables.Add(parentItem);

		TreeDataGridSource = OutputsCoinListDataGridSource.Create(new List<OutputsCoinListItem> { parentItem });
		TreeDataGridSource.DisposeWith(_disposables);
	}

	public HierarchicalTreeDataGridSource<OutputsCoinListItem> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
