using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

public class CoinjoinCoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public CoinjoinCoinListViewModel(UiContext uiContext, IEnumerable<SmartCoin> availableCoins, Network network, int totalCoinsOnSideCount) : base(uiContext)
	{
		var coinItems = availableCoins.Select(x => new CoinjoinCoinViewModel(uiContext, x, network)).ToList();
		foreach (var coin in coinItems)
		{
			coin.IsChild = true;
		}

		if (coinItems.Count > 0)
		{
			coinItems.Last().IsLastChild = true;
		}

		var parentItem = new CoinjoinCoinViewModel(uiContext, coinItems.ToArray(), totalCoinsOnSideCount);
		coinItems.Insert(0, parentItem);
		_disposables.Add(parentItem);

		TreeDataGridSource = CoinjoinCoinListDataGridSource.Create(new List<CoinjoinCoinListItem> { parentItem });
		TreeDataGridSource.DisposeWith(_disposables);
	}

	public HierarchicalTreeDataGridSource<CoinjoinCoinListItem> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
