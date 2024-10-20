using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Controls;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public class InputsCoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public InputsCoinListViewModel(IEnumerable<SmartCoin> availableCoins, int totalCoinsOnSideCount, bool? isExpanded = null, int? oldInputCount = null)
	{
		var coinItems = availableCoins.OrderByDescending(x => x.Amount).Select(x => new InputsCoinViewModel(x)).ToList();
		foreach (var coin in coinItems)
		{
			coin.IsChild = true;
		}

		if (coinItems.Count > 0)
		{
			coinItems.Last().IsLastChild = true;
		}

		var parentItem = new InputsCoinViewModel(coinItems.ToArray(), totalCoinsOnSideCount, isExpanded.GetValueOrDefault(), oldInputCount);
		coinItems.Insert(0, parentItem);
		_disposables.Add(parentItem);

		TreeDataGridSource = InputsCoinListDataGridSource.Create(new List<InputsCoinListItem> { parentItem });
		TreeDataGridSource.DisposeWith(_disposables);
	}

	public HierarchicalTreeDataGridSource<InputsCoinListItem> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
