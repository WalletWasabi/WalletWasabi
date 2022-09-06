using System.Collections;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.Details;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetails2ViewModel : RoutableViewModel
{
	public TransactionDetails2ViewModel(
		TransactionSummary transactionSummary,
		Wallet wallet,
		IObservable<Unit> updateTrigger)
	{
		Details = Observable.Return(transactionSummary)
			.Concat(updateTrigger.SelectMany(_ => Updates(transactionSummary.TransactionId, wallet)))
			.Select(GetProperties)
			.ObserveOn(RxApp.MainThreadScheduler)
			.ReplayLastActive();
	}

	public IObservable<IEnumerable> Details { get; }

	private static IEnumerable GetProperties(TransactionSummary transactionSummary)
	{
		return new object[]
		{
			new MoneyProperty("Amount", transactionSummary.Amount),
			new StringProperty("Block Index", transactionSummary.BlockIndex.ToString()),
			new StringProperty("Transaction Id", transactionSummary.TransactionId.ToString()),
			new StringProperty("Block Hash", transactionSummary.BlockHash?.ToString() ?? ""),
			new StringProperty("Date", transactionSummary.DateTime.ToString("D")),
		};
	}

	private static IObservable<TransactionSummary> Updates(uint256 txId, Wallet wallet)
	{
		var historyBuilder = new TransactionHistoryBuilder(wallet);
		return Observable
			.Start(historyBuilder.BuildHistorySummary, RxApp.TaskpoolScheduler)
			.Select(summaries => summaries.FirstOrDefault(summary => summary.TransactionId == txId))
			.WhereNotNull();
	}
}

internal class Property<T>
{
	public string Title { get; }
	public T Value { get; }

	public Property(string title, T value)
	{
		Title = title;
		Value = value;
	}
}

class MoneyProperty : Property<Money>
{
	public MoneyProperty(string title, Money value) : base(title, value)
	{
	}
}

class StringProperty : Property<string>
{
	public StringProperty(string title, string value) : base(title, value)
	{
	}
}
