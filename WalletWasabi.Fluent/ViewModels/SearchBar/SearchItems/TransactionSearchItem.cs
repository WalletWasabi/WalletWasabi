using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

internal class TransactionSearchItem
{
	public HistoryItemViewModelBase Transaction { get; }

	public TransactionSearchItem(HistoryItemViewModelBase transaction)
	{
		Transaction = transaction;
		Labels = transaction.Label.Labels.ToList();
		Date = transaction.Date;
		Id = transaction.Id.ToString();
		Amount = transaction.IncomingAmount ?? -transaction.OutgoingAmount ?? Money.Zero;
		Balance = transaction.Balance;
	}

	public Money Balance { get; }

	public string Id { get; }

	public DateTimeOffset Date { get; }

	public List<string> Labels { get; }

	public Money Amount { get; }
}
