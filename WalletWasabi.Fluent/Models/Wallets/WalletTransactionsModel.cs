using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using NBitcoin;
using ReactiveUI;
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
	private readonly ReadOnlyObservableCollection<TransactionSummary> _transactions;

	public WalletTransactionsModel(Wallet wallet)
	{
		_wallet = wallet;

		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(wallet, nameof(wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(wallet, nameof(wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);

		var transactionChanges =
			Observable.Defer(() => BuildSummary().ToObservable())
					  .Concat(TransactionProcessed.SelectMany(_ => BuildSummary()))
					  .ToObservableChangeSet(x => x.GetHash());

		transactionChanges.Bind(out _transactions).Subscribe();
	}

	public ReadOnlyObservableCollection<TransactionSummary> Transactions => _transactions;

	public IObservable<Unit> TransactionProcessed { get; }

	public bool TryGetById(uint256 transactionId, [NotNullWhen(true)] out TransactionSummary? transactionSummary)
	{
		var tryGetById = Transactions.FirstOrDefault(x => x.GetHash() == transactionId);

		if (tryGetById is null)
		{
			transactionSummary = default;
			return false;
		}

		transactionSummary = tryGetById;
		return true;
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
		return _wallet.BuildHistorySummary();
	}
}
