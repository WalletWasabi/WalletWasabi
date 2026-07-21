using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.FeeRateEstimation;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using static WalletWasabi.Logging.LoggerTools;

namespace WalletWasabi.Wallets;

public delegate Wallet WalletFactory(KeyManager keyManager);

public class Wallet : BackgroundService
{
	private readonly ComposedDisposable _disposables = new();

	public static WalletFactory CreateFactory(
		Network network, FilterStore filterStore, AllTransactionStore transactionStore, FilterHeaderChain filterHeaderChain,
		MempoolService mempoolService, ServiceConfiguration serviceConfiguration, BlockProvider blockProvider,
		EventBus eventBus, CpfpInfoProvider cpfpInfoProvider) =>
		keyManager => new Wallet(network, keyManager, filterStore, transactionStore, filterHeaderChain, blockProvider, mempoolService, serviceConfiguration, cpfpInfoProvider, eventBus);

	private Wallet(
		Network network,
		KeyManager keyManager,
		FilterStore filterStore,
		AllTransactionStore transactionStore,
		FilterHeaderChain filterHeaderChain,
		BlockProvider blockProvider,
		MempoolService mempoolService,
		ServiceConfiguration serviceConfiguration,
		CpfpInfoProvider cpfpInfoProvider,
		EventBus eventBus)
	{
		Password = "";
		Network = network;
		KeyManager = keyManager;
		ServiceConfiguration = serviceConfiguration;
		CpfpInfoProvider = cpfpInfoProvider;
		DestinationProvider = new InternalDestinationProvider(KeyManager);
		_filterStore = filterStore;
		TransactionStore = transactionStore;
		FilterHeaderChain = filterHeaderChain;

		TransactionProcessor = new TransactionProcessor(TransactionStore, mempoolService, keyManager, ServiceConfiguration.DustThreshold, eventBus);
		WalletFilterProcessor = new WalletFilterProcessor(keyManager, TransactionStore, _filterStore, FilterHeaderChain, TransactionProcessor, blockProvider, eventBus);
		Coins = TransactionProcessor.Coins;
		BatchedPayments = new PaymentBatch();
		OutputProvider = new PaymentAwareOutputProvider(DestinationProvider, BatchedPayments, RandomnessProviders.Secure);
		_eventBus = eventBus;
		WalletId = new WalletId(Guid.NewGuid());

		_eventBus.Subscribe<MiningFeeRatesChanged>(e => FeeRateEstimations = e.AllFeeEstimate)
			.DisposeUsing(_disposables);
		_eventBus.Subscribe<WalletRelevantTransactionProcessed>(e =>
		{
			if (e.WalletName == WalletName)
			{
				WalletRelevantTransactionProcessed(e.Result);
			}
		})
			.DisposeUsing(_disposables);
		_eventBus.Subscribe<NewTransactionInMempool>(e => Mempool_TransactionReceived(e.Transaction))
			.DisposeUsing(_disposables);
		_eventBus.Subscribe<FilterProcessed>(e => _lastFilterProcess = e.Filter.Header.Height)
			.DisposeUsing(_disposables);
	}

	private readonly EventBus _eventBus;
	private readonly FilterStore _filterStore;
	private ChainHeight _lastFilterProcess = 0;
	public AllTransactionStore TransactionStore { get; }
	public FilterHeaderChain FilterHeaderChain { get; }

	public WalletId WalletId { get; }
	public bool Loaded { get; private set; }
	public KeyManager KeyManager { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	public FeeRateEstimations? FeeRateEstimations { get; private set; }
	public string WalletName => KeyManager.WalletName;

	public CoinsRegistry Coins { get; }

	public bool NonPrivateCoinIsolation => KeyManager.NonPrivateCoinIsolation;

	public Network Network { get; }
	public TransactionProcessor TransactionProcessor { get; }

	public CpfpInfoProvider CpfpInfoProvider { get; }
	public WalletFilterProcessor WalletFilterProcessor { get; }

	public bool IsLoggedIn { get; private set; }
	public string Password { get; set; }

	public IKeyChain? KeyChain { get; private set; }

	public IDestinationProvider DestinationProvider { get; }

	public OutputProvider OutputProvider { get; }
	public PaymentBatch BatchedPayments { get; }

	public int AnonScoreTarget => KeyManager.AnonScoreTarget;
	public bool ConsolidationMode { get; set; }

	public Money PlebStopThreshold => KeyManager.PlebStopThreshold;

	public ICoinsView GetAllCoins() => Coins.AsAllCoinsView();

	public bool IsWalletPrivate() => GetPrivacyPercentage() >= 100;

	public IEnumerable<SmartCoin> GetCoinjoinCoinCandidates() => Coins;

	/// <summary>
	/// Get all the transactions associated to the wallet ordered by blockchain.
	/// </summary>
	public IEnumerable<SmartTransaction> GetTransactions()
	{
		var walletTransactions = new HashSet<SmartTransaction>();

		foreach (SmartCoin coin in GetAllCoins())
		{
			walletTransactions.Add(coin.Transaction);
			if (coin.SpenderTransaction is not null)
			{
				walletTransactions.Add(coin.SpenderTransaction);
			}
		}

		return walletTransactions.OrderByBlockchain().ToList();
	}

	/// <summary>
	/// Get all wallet transactions along with corresponding amounts ordered by blockchain.
	/// </summary>
	/// <param name="sortForUi"><c>true</c> to sort by "first seen", "height", and "block index", <c>false</c> to sort by "height", "block index", and "first seen".</param>
	/// <remarks>Transaction amount specifies how it affected your final wallet balance (spend some bitcoin, received some bitcoin, or no change).</remarks>
	public async Task<List<TransactionSummary>> BuildHistorySummaryAsync(bool sortForUi = false, CancellationToken cancellationToken = default)
	{
		var cpfpInfos = await CpfpInfoProvider.GetCachedCpfpInfoAsync(cancellationToken).ConfigureAwait(false);

		Dictionary<uint256, TransactionSummary> mapByTxid = new();

		foreach (SmartCoin coin in GetAllCoins())
		{
			if (mapByTxid.TryGetValue(coin.TransactionId, out TransactionSummary? found)) // If found then update.
			{
				found.Amount += coin.Amount;
			}
			else
			{
				FeeRate? effectiveFeeRate = null;
				if (cpfpInfos.FirstOrDefault(x => x.Transaction == coin.Transaction) is { } cachedCpfpInfo)
				{
					effectiveFeeRate = new FeeRate(cachedCpfpInfo.CpfpInfo.EffectiveFeePerVSize);
				}

				mapByTxid.Add(coin.TransactionId, new TransactionSummary(coin.Transaction, coin.Amount, effectiveFeeRate));
			}

			if (coin.SpenderTransaction is { } spenderTransaction)
			{
				var spenderTxId = spenderTransaction.GetHash();

				if (mapByTxid.TryGetValue(spenderTxId, out TransactionSummary? foundSpenderCoin)) // If found then update.
				{
					foundSpenderCoin.Amount -= coin.Amount;
				}
				else
				{
					FeeRate? effectiveFeeRate = null;
					if (cpfpInfos.FirstOrDefault(x => x.Transaction == coin.Transaction) is { } cachedCpfpInfo)
					{
						effectiveFeeRate = new FeeRate(cachedCpfpInfo.CpfpInfo.EffectiveFeePerVSize);
					}

					mapByTxid.Add(spenderTxId, new TransactionSummary(spenderTransaction, Money.Zero - coin.Amount, effectiveFeeRate));
				}
			}
		}

		return sortForUi
			? mapByTxid.Values.OrderBy(x => x.FirstSeen).ThenBy(x => x.Height).ThenBy(x => x.BlockIndex).ToList()
			: mapByTxid.Values.OrderByBlockchain().ToList();
	}

	public HdPubKey GetNextReceiveAddress(IEnumerable<string> destinationLabels, ScriptPubKeyType scriptPubKeyType)
	{
		return KeyManager.GetNextReceiveKey(new LabelsArray(destinationLabels), scriptPubKeyType);
	}

	public int GetPrivacyPercentage()
	{
		var currentPrivacyScore = Coins.Sum(x => x.Amount.Satoshi * Math.Min(x.HdPubKey.AnonymitySet - 1, x.IsPrivate(AnonScoreTarget) ? AnonScoreTarget - 1 : AnonScoreTarget - 2));
		var maxPrivacyScore = Coins.TotalAmount().Satoshi * (AnonScoreTarget - 1);
		int pcPrivate = maxPrivacyScore == 0M ? 0 : (int)(currentPrivacyScore * 100 / maxPrivacyScore);

		return pcPrivate;
	}

	public bool TryLogin(string password, out string? compatibilityPasswordUsed)
	{
		compatibilityPasswordUsed = null;

		if (KeyManager.IsWatchOnly)
		{
			IsLoggedIn = true;
			Password = "";
		}
		else if (PasswordHelper.TryPassword(KeyManager, password, out compatibilityPasswordUsed))
		{
			IsLoggedIn = true;
			Password = compatibilityPasswordUsed ?? Guard.Correct(password);
			KeyChain = new KeyChain(KeyManager, Password);
		}

		return IsLoggedIn;
	}

	public void Logout()
	{
		IsLoggedIn = false;
	}

	/// <inheritdoc/>
	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		await WalletFilterProcessor.StartAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogTrace(FormatLog("Wallet filter processor is started.", this));

		await LoadWalletStateAsync(cancellationToken).ConfigureAwait(false);
		Logger.LogTrace(FormatLog("State is loaded.", this));

		LoadDummyMempool();
		LoadExcludedCoins();

		await base.StartAsync(cancellationToken).ConfigureAwait(false);

		Loaded = true;
		_eventBus.Publish(new WalletLoaded(this));
	}

	private void LoadExcludedCoins()
	{
		bool isUpdateRequired = false;
		foreach (var excludedCoin in KeyManager.ExcludedCoinsFromCoinJoin)
		{
			var coin = Coins.SingleOrDefault(c => c.Outpoint == excludedCoin);
			if (coin != null)
			{
				coin.IsExcludedFromCoinJoin = true;
			}
			else
			{
				isUpdateRequired = true;
			}
		}
		if (isUpdateRequired)
		{
			UpdateExcludedCoinFromCoinJoin();
		}
	}

	/// <inheritdoc />
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		Logger.LogInfo(FormatLog("is fully synchronized.", this));
	}

	public string AddCoinJoinPayment(IDestination destination, Money amount)
	{
		var paymentId = BatchedPayments.AddPayment(destination, amount);
		_eventBus.Publish(new PaymentBatchChanged(BatchedPayments));
		return paymentId.ToString();
	}

	public void CancelCoinJoinPayment(Guid paymentId)
	{
		BatchedPayments.AbortPayment(paymentId);
		_eventBus.Publish(new PaymentBatchChanged(BatchedPayments));
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancel)
	{
		await base.StopAsync(cancel).ConfigureAwait(false);
		await WalletFilterProcessor.StopAsync(cancel).ConfigureAwait(false);
		WalletFilterProcessor.Dispose();

		_disposables.Dispose();
	}

	private void WalletRelevantTransactionProcessed(ProcessedResult e)
	{
		try
		{
			if (e.Transaction.CanBeSpeedUpUsingCpfp())
			{
				CpfpInfoProvider.ScheduleRequest(e.Transaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(FormatLog(ex.ToString(), this));
		}
	}

	private void Mempool_TransactionReceived(SmartTransaction tx)
	{
		try
		{
			if (!TransactionProcessor.IsAware(tx.GetHash()))
			{
				TransactionProcessor.Process(tx);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(FormatLog(ex.ToString(), this));
		}
	}

	private async Task LoadWalletStateAsync(CancellationToken cancellationToken)
	{
		// Make sure that the keys are asserted in case of an empty HdPubKeys array.
		KeyManager.GetKeys();

		TransactionProcessor.Process(TransactionStore.ConfirmedStore.GetTransactions());

		int i = 0;
		while (_lastFilterProcess < FilterHeaderChain.ServerTipHeight)
		{
			i++;

			// Every ten seconds, log a message to indicate that the wallet is waiting for filters to be processed.
			if (i % 100 == 0)
			{
				Logger.LogDebug(FormatLog($"Waiting until filters are processed ({_lastFilterProcess} < {FilterHeaderChain.ServerTipHeight})", this));
			}

			await Task.Delay(100, cancellationToken).ConfigureAwait(false);
		}

		Logger.LogTrace(FormatLog("Waiting for initial synchronization to finish.", this));
		await WalletFilterProcessor.InitialSynchronizationFinished.WaitAsync(cancellationToken).ConfigureAwait(false);
	}

	private void LoadDummyMempool()
	{
		if (TransactionStore.MempoolStore.IsEmpty())
		{
			return;
		}

		// Only clean the mempool if we're fully synchronized.
		if (FilterHeaderChain.HashesLeft == 0)
		{
			var txsToProcess = new List<SmartTransaction>();
			foreach (var tx in TransactionStore.MempoolStore.GetTransactions())
			{
				var txid = tx.GetHash();
				if (DateTimeOffset.UtcNow - tx.FirstSeen < TimeSpan.FromDays(ServiceConfiguration.DropUnconfirmedTransactionsAfterDays))
				{
					txsToProcess.Add(tx);
				}
				else
				{
					if (TransactionStore.MempoolStore.TryRemove(txid, out _))
					{
						Logger.LogInfo(FormatLog($"Transaction {txid} dropped after {ServiceConfiguration.DropUnconfirmedTransactionsAfterDays} days being unconfirmed.", this));
					}
				}
			}

			TransactionProcessor.Process(txsToProcess);
		}
		else
		{
			TransactionProcessor.Process(TransactionStore.MempoolStore.GetTransactions());
		}
	}

	public void ExcludeCoinFromCoinJoin(OutPoint outpoint, bool exclude = true)
	{
		if (!Coins.TryGetByOutPoint(outpoint, out var coin))
		{
			throw new InvalidOperationException($"Coin '{outpoint}' doesn't belong to the wallet or is spent.");
		}

		coin.IsExcludedFromCoinJoin = exclude;
		UpdateExcludedCoinFromCoinJoin();
	}

	public void UpdateExcludedCoinsFromCoinJoin(OutPoint[] outPointsToExclude)
	{
		foreach (var coin in Coins)
		{
			coin.IsExcludedFromCoinJoin = outPointsToExclude.Contains(coin.Outpoint);
		}

		UpdateExcludedCoinFromCoinJoin();
	}

	private void UpdateExcludedCoinFromCoinJoin()
	{
		var excludedOutpoints = Coins.Where(c => c.IsExcludedFromCoinJoin).Select(c => c.Outpoint);
		KeyManager.SetExcludedCoinsFromCoinJoin(excludedOutpoints);
	}

	public void UpdateUsedHdPubKeysLabels(Dictionary<HdPubKey, LabelsArray> hdPubKeysWithLabels)
	{
		if (hdPubKeysWithLabels.Count == 0)
		{
			return;
		}

		foreach (var item in hdPubKeysWithLabels)
		{
			item.Key.SetLabel(item.Value);
		}

		KeyManager.ToFile();
	}
}
