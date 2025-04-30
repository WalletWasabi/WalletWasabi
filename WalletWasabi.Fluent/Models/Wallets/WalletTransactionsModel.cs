using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.Models.Wallets;

[AutoInterface]
public partial class WalletTransactionsModel : ReactiveObject, IDisposable
{
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;
	private readonly TransactionTreeBuilder _treeBuilder;
	private readonly CompositeDisposable _disposable = new();

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

		NewTransactionArrived =
			Observable.FromEventPattern<ProcessedResult>(wallet, nameof(wallet.WalletRelevantTransactionProcessed))
					  .Select(x => (walletModel, x.EventArgs))
					  .ObserveOn(RxApp.MainThreadScheduler);

		RequestedCpfpInfoArrived = wallet.CpfpInfoProvider is null ? null :
			Observable.FromEventPattern<EventArgs>(wallet.CpfpInfoProvider, nameof(wallet.CpfpInfoProvider.RequestedCpfpInfoArrived)).ToSignal()
				.ObserveOn(RxApp.MainThreadScheduler);

		Cache = (RequestedCpfpInfoArrived is null ? TransactionProcessed : TransactionProcessed.Merge(RequestedCpfpInfoArrived))
			.FetchAsync(() => BuildSummaryAsync(CancellationToken.None), model => model.Id)
			.DisposeWith(_disposable);

		IsEmpty = Cache.Empty();
	}

	public IObservableCache<TransactionModel, uint256> Cache { get; set; }

	public IObservable<bool> IsEmpty { get; }

	public IObservable<Unit> TransactionProcessed { get; }

	public IObservable<(IWalletModel Wallet, ProcessedResult EventArgs)> NewTransactionArrived { get; }
	public IObservable<Unit>? RequestedCpfpInfoArrived { get; }

	public bool TryGetById(uint256 transactionId, bool isChild, [NotNullWhen(true)] out TransactionModel? transaction)
	{
		var result = isChild
			? Cache.Items.SelectMany(x => x.Children).FirstOrDefault(x => x.Id == transactionId)
			: Cache.Items.FirstOrDefault(x => x.Id == transactionId);

		if (result is null)
		{
			transaction = default;
			return false;
		}

		transaction = result;
		return true;
	}

	public async Task<SmartTransaction> LoadFromFileAsync(string path)
	{
		var txn = await TransactionHelpers.ParseTransactionAsync(path, _wallet.Network);
		return txn;
	}

	public async Task<TimeSpan?> TryEstimateConfirmationTimeAsync(uint256 id, CancellationToken cancellationToken)
	{
		if (!_wallet.BitcoinStore.TransactionStore.TryGetTransaction(id, out var smartTransaction))
		{
			throw new InvalidOperationException($"Transaction not found! ID: {id}");
		}

		return await TransactionFeeHelper.EstimateConfirmationTimeAsync(_wallet.FeeRateEstimations, _wallet.Network, smartTransaction, _wallet.CpfpInfoProvider, cancellationToken);
	}

	public async Task<TimeSpan?> TryEstimateConfirmationTimeAsync(TransactionModel model, CancellationToken cancellationToken) => await TryEstimateConfirmationTimeAsync(model.Id, cancellationToken);

	public TimeSpan? TryEstimateConfirmationTime(TransactionInfo info)
	{
		TransactionFeeHelper.TryEstimateConfirmationTime(_wallet, info.FeeRate, out var estimate);
		return estimate;
	}

	public TransactionInfo Create(string address, decimal amount, string label) =>
		Create(address, amount, new LabelsArray(label));

	public TransactionInfo Create(string address, decimal amount, LabelsArray labels)
	{
		var transactionInfo = new TransactionInfo(BitcoinAddress.Create(address, _wallet.Network), _walletModel.Settings.AnonScoreTarget)
		{
			Amount = new Money(amount, MoneyUnit.BTC),
			Recipient = new LabelsArray("Buy Anything Agent"),
			IsFixedAmount = true
		};

		return transactionInfo;
	}

	public async Task<SpeedupTransaction> CreateSpeedUpTransactionAsync(TransactionModel transaction, CancellationToken cancellationToken)
	{
		if (!_wallet.BitcoinStore.TransactionStore.TryGetTransaction(transaction.Id, out var targetTransaction))
		{
			throw new InvalidOperationException($"Transaction not found! ID: {transaction.Id}");
		}

		// If the transaction has CPFPs, then we want to speed them up instead of us.
		// Although this does happen inside the SpeedUpTransaction method, but we want to give the tx that was actually sped up to SpeedUpTransactionDialog.
		if (targetTransaction.TryGetLargestCPFP(_wallet.KeyManager, out var largestCpfp))
		{
			targetTransaction = largestCpfp;
		}
		var boostingTransaction = await _wallet.SpeedUpTransactionAsync(targetTransaction, null, cancellationToken);

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
		if (!_wallet.BitcoinStore.TransactionStore.TryGetTransaction(transaction.Id, out var targetTransaction))
		{
			throw new InvalidOperationException($"Transaction not found! ID: {transaction.Id}");
		}

		var cancellingTransaction = _wallet.CancelTransaction(targetTransaction);

		return new CancellingTransaction(transaction, cancellingTransaction, _walletModel.AmountProvider.Create(cancellingTransaction.Fee));
	}

	public async Task<BuildTransactionResult> BuildTransactionForSIBAsync(TransactionInfo transactionInfo, bool isPayJoin = false, bool tryToSign = true)
	{
		return await Task.Run(() =>
		{
			if (transactionInfo.IsOptimized)
			{
				return _wallet.BuildChangelessTransaction(
					transactionInfo.Destination,
					transactionInfo.Recipient,
					transactionInfo.FeeRate,
					transactionInfo.ChangelessCoins,
					tryToSign: tryToSign);
			}

			if (isPayJoin && transactionInfo.SubtractFee)
			{
				throw new InvalidOperationException("Not possible to subtract the fee.");
			}

			return _wallet.BuildTransactionForSIB(
				transactionInfo.Destination,
				transactionInfo.Amount,
				transactionInfo.Recipient,
				transactionInfo.SubtractFee,
				isPayJoin ? transactionInfo.PayJoinClient : null,
				tryToSign: tryToSign);
		});
	}

	public Task SendAsync(SpeedupTransaction speedupTransaction) => SendAsync(speedupTransaction.BoostingTransaction);

	public Task SendAsync(CancellingTransaction cancellingTransaction) => SendAsync(cancellingTransaction.CancelTransaction);

	public async Task SendAsync(BuildTransactionResult transaction)
	{
		await Services.TransactionBroadcaster.SendTransactionAsync(transaction.Transaction);
		_wallet.UpdateUsedHdPubKeysLabels(transaction.HdPubKeysWithNewLabels);
	}

	private async Task<IEnumerable<TransactionModel>> BuildSummaryAsync(CancellationToken cancellationToken)
	{
		var orderedRawHistoryList = await _wallet.BuildHistorySummaryAsync(sortForUi: true, cancellationToken: cancellationToken);
		var transactionModels = await _treeBuilder.BuildAsync(orderedRawHistoryList, cancellationToken);
		return transactionModels;
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

	public IEnumerable<BitcoinAddress> GetDestinationAddresses(uint256 id)
	{
		if (!_wallet.BitcoinStore.TransactionStore.TryGetTransaction(id, out var smartTransaction))
		{
			throw new InvalidOperationException($"Transaction not found! ID: {id}");
		}

		List<IInput> inputs = smartTransaction.GetInputs().ToList();
		List<Output> outputs = smartTransaction.GetOutputs(_wallet.Network).ToList();

		return GetDestinationAddresses(inputs, outputs);
	}

	private IEnumerable<BitcoinAddress> GetDestinationAddresses(ICollection<IInput> inputs, ICollection<Output> outputs)
	{
		var myOwnInputs = inputs.OfType<KnownInput>().ToList();
		var foreignInputs = inputs.OfType<ForeignInput>().ToList();
		var myOwnOutputs = outputs.OfType<OwnOutput>().ToList();
		var foreignOutputs = outputs.OfType<ForeignOutput>().ToList();

		// All inputs and outputs are my own, transaction is a self-spend.
		if (foreignInputs.Count == 0 && foreignOutputs.Count == 0)
		{
			// Classic self-spend to one or more external addresses.
			if (myOwnOutputs.Any(x => !x.IsInternal))
			{
				// Destinations are the external addresses.
				return myOwnOutputs.Where(x => !x.IsInternal).Select(x => x.DestinationAddress);
			}

			// Edge-case: self-spend to one or more internal addresses.
			// We can't know the destinations, return all the outputs.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}

		// All inputs are foreign but some outputs are my own, someone is sending coins to me.
		if (myOwnInputs.Count == 0 && myOwnOutputs.Count != 0)
		{
			// All outputs that are my own are the destinations.
			return myOwnOutputs.Select(x => x.DestinationAddress);
		}

		// I'm sending a transaction to someone else.
		// All outputs that are not my own are the destinations.
		return foreignOutputs.Select(x => x.DestinationAddress);
	}

	public void Dispose() => _disposable.Dispose();
}
