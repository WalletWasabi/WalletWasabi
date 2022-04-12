using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar.SearchItem;

internal class TransactionSearchItem
{
	public HistoryItemViewModelBase Transaction { get; }

	public TransactionSearchItem(HistoryItemViewModelBase transaction)
	{
		Transaction = transaction;
		Labels = transaction.Label;
		Date = transaction.Date;
		Id = transaction.Id.ToString();
		Amount = transaction.IncomingAmount ?? -transaction.OutgoingAmount ?? Money.Zero;
		Balance = transaction.Balance;
	}

	public Money Balance { get; }

	public string Id { get; }

	public DateTimeOffset Date { get; set; }

	public List<string> Labels { get; set; }

	public Money Amount { get; }
}