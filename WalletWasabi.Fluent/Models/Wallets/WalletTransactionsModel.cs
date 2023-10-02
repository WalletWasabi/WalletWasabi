using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletTransactionsModel : ReactiveObject
{
	private readonly Wallet _wallet;

	public WalletTransactionsModel(Wallet wallet)
	{
		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(wallet, nameof(wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(wallet, nameof(wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		List =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(TransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.GetHash());
		_wallet = wallet;
	}

	public IObservable<Unit> TransactionProcessed { get; }

	public IObservable<IChangeSet<TransactionSummary, uint256>> List { get; }

	public async Task<TransactionSummary?> GetById(string transactionId)
	{
		var txRecordList = await Task.Run(() => TransactionHistoryBuilder.BuildHistorySummary(_wallet));

		var transaction = txRecordList.FirstOrDefault(x => x.GetHash().ToString() == transactionId);
		return transaction;
	}

	public TimeSpan? TryEstimateConfirmationTime(TransactionSummary transactionSummary)
	{
		return
			TransactionFeeHelper.TryEstimateConfirmationTime(_wallet, transactionSummary.Transaction, out var estimate)
			? estimate
			: null;
	}

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return TransactionHistoryBuilder.BuildHistorySummary(_wallet);
	}
}
