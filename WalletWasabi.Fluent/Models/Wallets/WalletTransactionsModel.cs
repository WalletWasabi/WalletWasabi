using NBitcoin;
using ReactiveUI;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
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

	public WalletTransactionsModel(Wallet wallet)
	{
		_wallet = wallet;

		TransactionProcessed =
			Observable.FromEventPattern<ProcessedResult?>(wallet, nameof(wallet.WalletRelevantTransactionProcessed)).ToSignal()
					  .Merge(Observable.FromEventPattern(wallet, nameof(wallet.NewFiltersProcessed)).ToSignal())
					  .Sample(TimeSpan.FromSeconds(1))
					  .ObserveOn(RxApp.MainThreadScheduler)
					  .StartWith(Unit.Default);
	}

	public IObservable<Unit> TransactionProcessed { get; }

	public bool TryGetById(uint256 transactionId, [NotNullWhen(true)] out TransactionSummary? transactionSummary)
		=> _wallet.TryGetTransactionSummary(transactionId, out transactionSummary);

	public TimeSpan? TryEstimateConfirmationTime(TransactionSummary transactionSummary)
	{
		return
			TransactionFeeHelper.TryEstimateConfirmationTime(_wallet, transactionSummary.Transaction, out var estimate)
			? estimate
			: null;
	}
}
