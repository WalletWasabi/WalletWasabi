using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletTransactionsModel : ReactiveObject
{
	private readonly Wallet _wallet;
	private readonly TransactionTreeBuilder _treeBuilder;

	public WalletTransactionsModel(Wallet wallet)
	{
		_wallet = wallet;
		_treeBuilder = new TransactionTreeBuilder(wallet);

		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(wallet, nameof(wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(wallet, nameof(wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);
	}

	public IObservable<Unit> TransactionProcessed { get; }

	public IObservable<IChangeSet<TransactionModel, uint256>> List => TransactionProcessed.ProjectList(BuildSummary, x => x.Id);

	public async Task<TransactionSummary?> GetById(string transactionId)
	{
		var txRecordList = await Task.Run(() => _wallet.BuildHistorySummary());

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

	public (SmartTransaction TransactionToSpeedUp, BuildTransactionResult BoostingTransaction) CreateSpeedUpTransaction(TransactionModel transaction)
	{
		var transactionToSpeedUp = transaction.TransactionSummary.Transaction;

		// If the transaction has CPFPs, then we want to speed them up instead of us.
		// Although this does happen inside the SpeedUpTransaction method, but we want to give the tx that was actually sped up to SpeedUpTransactionDialog.
		if (transactionToSpeedUp.TryGetLargestCPFP(_wallet.KeyManager, out var largestCpfp))
		{
			transactionToSpeedUp = largestCpfp;
		}
		var boostingTransaction = _wallet.SpeedUpTransaction(transactionToSpeedUp);

		return (transactionToSpeedUp, boostingTransaction);
	}

	public BuildTransactionResult CreateCancellingTransaction(TransactionModel transaction)
	{
		var transactionToCancel = transaction.TransactionSummary.Transaction;
		var cancellingTransaction = _wallet.CancelTransaction(transactionToCancel);
		return cancellingTransaction;
	}

	private IEnumerable<TransactionModel> BuildSummary()
	{
		var rawHistoryList = _wallet.BuildHistorySummary();

		var orderedRawHistoryList =
			rawHistoryList.OrderBy(x => x.FirstSeen)
						  .ThenBy(x => x.Height)
						  .ThenBy(x => x.BlockIndex)
						  .ToList();

		return _treeBuilder.Build(orderedRawHistoryList);
	}
}
