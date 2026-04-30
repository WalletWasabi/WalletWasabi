using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Transactions.Inputs;

public class InputsCoinListViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public InputsCoinListViewModel(UiContext uiContext, IEnumerable<SmartCoin> availableCoins, Network network, int inputCount, bool? isExpanded = null, int? oldInputCount = null) : base(uiContext)
	{
		var coinItems = availableCoins.OrderByDescending(x => x.Amount).Select(x => new InputsCoinViewModel(uiContext, x, network)).ToList();
		foreach (var coin in coinItems)
		{
			coin.IsChild = true;
		}

		if (coinItems.Count > 0)
		{
			coinItems.Last().IsLastChild = true;
		}

		if (oldInputCount is not null)
		{
			NbDiff = inputCount - oldInputCount;
		}

		var parentItem = new InputsCoinViewModel(uiContext, coinItems.ToArray(), inputCount, isExpanded.GetValueOrDefault(), NbDiff);
		coinItems.Insert(0, parentItem);
		_disposables.Add(parentItem);

		TreeDataGridSource = InputsCoinListDataGridSource.Create(new List<InputsCoinListItem> { parentItem });
		TreeDataGridSource.DisposeWith(_disposables);
	}

	public int? NbDiff { get; }

	public HierarchicalTreeDataGridSource<InputsCoinListItem> TreeDataGridSource { get; }

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
