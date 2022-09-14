using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

internal class TransactionItemNode
{
	private TransactionItemNode(int index, HistoryItemViewModelBase item, TransactionItemNode? parent)
	{
		Index = index;
		Item = item;
		Parent = parent;
	}

	public int Index { get; }
	public TransactionItemNode? Parent { get; }
	public HistoryItemViewModelBase Item { get; }
	public IEnumerable<TransactionItemNode> Children { get; private set; } = null!;

	public static IEnumerable<TransactionItemNode> Create(IEnumerable<HistoryItemViewModelBase> transactions, TransactionItemNode? parent = null)
	{
		return transactions.Select(
			(x, i) =>
			{
				var parentNode = new TransactionItemNode(i, x, parent);
				var childNodes = Create(x.Children, parentNode);
				parentNode.Children = childNodes;
				return parentNode;
			});
	}
}
