using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Fluent.Models.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Outputs;

public class OutputsCoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public OutputsCoinListViewModel(List<TxOut> ownOutputs, List<TxOut> foreignOutputs, Network network, Script? destinationScript = null, bool? isExpanded = null, int? oldOutputCount = null)
	{

		var outputCount = ownOutputs.Count + foreignOutputs.Count;

		var coinItems = ownOutputs
			.Union(foreignOutputs)
			.OrderByDescending(x => x.Value)
			.Select(x =>
				new OutputsCoinViewModel(
					x,
					network,
					ownOutputs.Contains(x),
					destinationScript is not null && x.ScriptPubKey != destinationScript))
			.ToList();

		foreach (var coin in coinItems)
		{
			coin.IsChild = true;
		}

		if (coinItems.Count > 0)
		{
			coinItems.Last().IsLastChild = true;
		}

		if (oldOutputCount is not null)
		{
			NbDiff = outputCount - oldOutputCount;
		}

		var parentItem = new OutputsCoinViewModel(coinItems.ToArray(), outputCount, isExpanded.GetValueOrDefault(), NbDiff);
		coinItems.Insert(0, parentItem);
		_disposables.Add(parentItem);

		TreeDataGridSource = OutputsCoinListDataGridSource.Create(new List<OutputsCoinListItem> { parentItem });
		TreeDataGridSource.DisposeWith(_disposables);
	}

	public int? NbDiff { get; }

	public HierarchicalTreeDataGridSource<OutputsCoinListItem> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
