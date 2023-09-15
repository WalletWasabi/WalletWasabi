using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
	private readonly TransactionHistoryBuilder _historyBuilder;

	public WalletTransactionsModel(Wallet wallet)
	{
		_historyBuilder = new TransactionHistoryBuilder(wallet);

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

	public bool AreEnoughToCreateTransaction(TransactionInfo transactionInfo, IEnumerable<SmartCoin> coins)
	{
		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(_wallet.KeyManager, transactionInfo, _wallet.Coins, coins, _wallet.Kitchen.SaltSoup(), out _);
	}

	private IEnumerable<TransactionSummary> BuildSummary()
	{
		return _historyBuilder.BuildHistorySummary();
	}
}
