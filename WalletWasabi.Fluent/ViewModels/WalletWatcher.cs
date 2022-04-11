using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels;

public class WalletWatcher
{
	public WalletWatcher(WalletManager walletManager)
	{
		var added = Observable
			.FromEventPattern<Wallet>(Services.WalletManager, nameof(WalletManager.WalletAdded))
			.Select(_ => Unit.Default);
		var transactionProcessed = Observable
			.FromEventPattern<ProcessedResult>(Services.WalletManager, nameof(WalletManager.WalletRelevantTransactionProcessed))
			.Select(_ => Unit.Default);
	}

	public IObservable<TransactionSummary> Transactions => GetTransactions();

	private IObservable<TransactionSummary> GetTransactions()
	{
		return new[]
		{
			new TransactionSummary
			{
				Amount = Money.Zero,
				BlockIndex = 1,
				Height = 1,
				TransactionId = 123,
				IsOwnCoinjoin = true,
				DateTime = DateTimeOffset.Now,
				Label = new SmartLabel("Hola")
			}
		}.ToObservable();
	}
}