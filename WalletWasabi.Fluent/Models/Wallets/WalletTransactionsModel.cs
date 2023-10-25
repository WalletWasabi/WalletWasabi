using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Wallets;
#pragma warning disable CA2000

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletTransactionsModel : ReactiveObject, IDisposable
{
	private readonly Wallet _wallet;
	private readonly TransactionTreeBuilder _treeBuilder;
	private readonly ReadOnlyObservableCollection<TransactionModel> _transactions;
	private readonly CompositeDisposable _disposable = new();

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

		var retriever =
			new Retriever<TransactionModel, uint256>(TransactionProcessed, model => model.Id, BuildSummary)
				.DisposeWith(_disposable);

		retriever.Changes.Bind(out _transactions)
			.Subscribe()
			.DisposeWith(_disposable);

		IsEmpty = retriever.Changes
			.ToCollection()
			.Select(models => !models.Any());
	}

	public ReadOnlyObservableCollection<TransactionModel> List => _transactions;

	public IObservable<bool> IsEmpty { get; }

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
		var orderedRawHistoryList = _wallet.BuildHistorySummary(sortForUI: true);
		var transactionModels = _treeBuilder.Build(orderedRawHistoryList);
		return transactionModels;
	}

	public void Dispose() => _disposable.Dispose();
}
