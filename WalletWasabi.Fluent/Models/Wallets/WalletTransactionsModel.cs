using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;
	private readonly TransactionTreeBuilder _treeBuilder;
	private readonly ReadOnlyObservableCollection<TransactionModel> _transactions;

	public WalletTransactionsModel(IWalletModel walletModel, Wallet wallet)
	{
		_walletModel = walletModel;
		_wallet = wallet;
		_treeBuilder = new TransactionTreeBuilder(wallet);

		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(wallet, nameof(wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(wallet, nameof(wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		var transactionChanges =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(TransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.Id);

		transactionChanges.Bind(out _transactions).Subscribe(set => { });
	}

	public ReadOnlyObservableCollection<TransactionModel> List => _transactions;

	public IObservable<Unit> TransactionProcessed { get; }

	public bool TryGetById(uint256 transactionId, [NotNullWhen(true)] out TransactionModel? transaction)
	{
		var result = List.FirstOrDefault(x => x.Id == transactionId);

		if (result is null)
		{
			transaction = default;
			return false;
		}

		transaction = result;
		return true;
	}

	public TimeSpan? TryEstimateConfirmationTime(TransactionSummary transactionSummary)
	{
		return
			TransactionFeeHelper.TryEstimateConfirmationTime(_wallet, transactionSummary.Transaction, out var estimate)
			? estimate
			: null;
	}

	public SpeedupTransaction CreateSpeedUpTransaction(TransactionModel transaction)
	{
		var targetTransaction = transaction.TransactionSummary.Transaction;

		// If the transaction has CPFPs, then we want to speed them up instead of us.
		// Although this does happen inside the SpeedUpTransaction method, but we want to give the tx that was actually sped up to SpeedUpTransactionDialog.
		if (targetTransaction.TryGetLargestCPFP(_wallet.KeyManager, out var largestCpfp))
		{
			targetTransaction = largestCpfp;
		}
		var boostingTransaction = _wallet.SpeedUpTransaction(targetTransaction);

		var fee = _walletModel.AmountProvider.Create(GetFeeDifference(targetTransaction, boostingTransaction));

		var originalForeignAmounts = targetTransaction.ForeignOutputs.Select(x => x.TxOut.Value).OrderBy(x => x).ToArray();
		var boostedForeignAmounts = boostingTransaction.Transaction.ForeignOutputs.Select(x => x.TxOut.Value).OrderBy(x => x).ToArray();

		// Note, if it's CPFP, then it is changed, but we shouldn't bother by it, due to the other condition.
		var areForeignAmountsUnchanged = originalForeignAmounts.SequenceEqual(boostedForeignAmounts);

		// If the foreign outputs are unchanged or we have an output, then we are paying the fee.
		var areWePayingTheFee = areForeignAmountsUnchanged || boostingTransaction.Transaction.GetWalletOutputs(_wallet.KeyManager).Any();

		return new SpeedupTransaction(targetTransaction, boostingTransaction, areWePayingTheFee, fee);
	}

	public CancellingTransaction CreateCancellingTransaction(TransactionModel transaction)
	{
		var targetTransaction = transaction.TransactionSummary.Transaction;
		var cancellingTransaction = _wallet.CancelTransaction(targetTransaction);

		return new CancellingTransaction(transaction, cancellingTransaction, _walletModel.AmountProvider.Create(cancellingTransaction.Fee));
	}

	public Task SendAsync(SpeedupTransaction speedupTransaction) => SendAsync(speedupTransaction.BoostingTransaction);

	public Task SendAsync(CancellingTransaction cancellingTransaction) => SendAsync(cancellingTransaction.CancelTransaction);

	public async Task SendAsync(BuildTransactionResult transaction)
	{
		await Services.TransactionBroadcaster.SendTransactionAsync(transaction.Transaction);
		_wallet.UpdateUsedHdPubKeysLabels(transaction.HdPubKeysWithNewLabels);
	}

	private IEnumerable<TransactionModel> BuildSummary()
	{
		var orderedRawHistoryList = _wallet.BuildHistorySummary(sortForUI: true);
		return _treeBuilder.Build(orderedRawHistoryList);
	}

	private Money GetFeeDifference(SmartTransaction transactionToSpeedUp, BuildTransactionResult boostingTransaction)
	{
		var isCpfp = boostingTransaction.Transaction.Transaction.Inputs.Any(x => x.PrevOut.Hash == transactionToSpeedUp.GetHash());
		var boostingTransactionFee = boostingTransaction.Fee;

		if (isCpfp)
		{
			return boostingTransactionFee;
		}

		var originalFee = transactionToSpeedUp.WalletInputs.Sum(x => x.Amount) - transactionToSpeedUp.OutputValues.Sum(x => x);
		return boostingTransactionFee - originalFee;
	}
}
