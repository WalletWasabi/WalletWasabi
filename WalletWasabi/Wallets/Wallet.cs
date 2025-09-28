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
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
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

namespace WalletWasabi.Wallets;

public class Wallet : BackgroundService, IWallet
{
	private volatile WalletState _state;
	private readonly IDisposable _feeRateSubscription;

	public Wallet(
		Network network,
		KeyManager keyManager,
		BitcoinStore bitcoinStore,
		ServiceConfiguration serviceConfiguration,
		TransactionProcessor transactionProcessor,
		WalletFilterProcessor walletFilterProcessor,
		CpfpInfoProvider cpfpInfoProvider,
		EventBus eventBus)
	{
		Network = network;
		KeyManager = keyManager;
		BitcoinStore = bitcoinStore;
		ServiceConfiguration = serviceConfiguration;
		CpfpInfoProvider = cpfpInfoProvider;

		DestinationProvider = new InternalDestinationProvider(KeyManager);

		TransactionProcessor = transactionProcessor;
		Coins = TransactionProcessor.Coins;
		WalletFilterProcessor = walletFilterProcessor;
		BatchedPayments = new PaymentBatch();
		OutputProvider = new PaymentAwareOutputProvider(DestinationProvider, BatchedPayments);
		WalletId = new WalletId(Guid.NewGuid());
		_feeRateSubscription =
			eventBus.Subscribe<MiningFeeRatesChanged>(e => FeeRateEstimations = e.AllFeeEstimate);
	}

	public event EventHandler<ProcessedResult>? WalletRelevantTransactionProcessed;

	public event EventHandler<FilterModel[]>? NewFiltersProcessed;

	public event EventHandler<WalletState>? StateChanged;

	public WalletId WalletId { get; }

	public WalletState State
	{
		get => _state;
		private set
		{
			if (_state == value)
			{
				return;
			}

			_state = value;
			StateChanged?.Invoke(this, _state);
		}
	}

	public BitcoinStore BitcoinStore { get; }
	public KeyManager KeyManager { get; }
	public ServiceConfiguration ServiceConfiguration { get; }
	public FeeRateEstimations FeeRateEstimations { get; private set; }
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

	public bool IsMixable =>
		State == WalletState.Started // Only running wallets
		&& KeyChain is not null; // that are not watch-only wallets and contain a keychain

	public Money PlebStopThreshold => KeyManager.PlebStopThreshold;

	public ICoinsView GetAllCoins() => Coins.AsAllCoinsView();

	public Task<bool> IsWalletPrivateAsync() => Task.FromResult(IsWalletPrivate());

	public bool IsWalletPrivate() => GetPrivacyPercentage() >= 100;

	public Task<IEnumerable<SmartTransaction>> GetTransactionsAsync() => Task.FromResult(GetTransactions());

	public Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync() => Task.FromResult(GetCoinjoinCoinCandidates());

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

	public void Initialize()
	{
		if (State > WalletState.WaitingForInit)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized} or {WalletState.WaitingForInit}. Current state: {State}.");
		}

		try
		{
			KeyManager.AssertNetworkOrClearBlockState(Network);
			EnsureHeightsAreAtLeastSegWitActivation();

			TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

			State = WalletState.Initialized;
		}
		catch
		{
			State = WalletState.Uninitialized;
			throw;
		}
	}

	/// <inheritdoc/>
	public override async Task StartAsync(CancellationToken cancel)
	{
		if (State != WalletState.Initialized)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Initialized}. Current state: {State}.");
		}

		try
		{
			State = WalletState.Starting;

			await WalletFilterProcessor.StartAsync(cancel).ConfigureAwait(false);

			await LoadWalletStateAsync(cancel).ConfigureAwait(false);
			LoadDummyMempool();
			LoadExcludedCoins();

			await base.StartAsync(cancel).ConfigureAwait(false);

			State = WalletState.Started;
		}
		catch
		{
			State = WalletState.Initialized;
			throw;
		}
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
		Logger.LogInfo($"Wallet '{WalletName}' is fully synchronized.");
	}

	public string AddCoinJoinPayment(IDestination destination, Money amount)
	{
		var paymentId = BatchedPayments.AddPayment(destination, amount);
		return paymentId.ToString();
	}

	/// <inheritdoc/>
	public override async Task StopAsync(CancellationToken cancel)
	{
		try
		{
			var prevState = State;
			State = WalletState.Stopping;

			if (prevState < WalletState.Stopping)
			{
				await base.StopAsync(cancel).ConfigureAwait(false);
				_feeRateSubscription.Dispose();
				if (prevState >= WalletState.Initialized)
				{
					await WalletFilterProcessor.StopAsync(cancel).ConfigureAwait(false);
					WalletFilterProcessor.Dispose();

					BitcoinStore.IndexStore.NewFilters -= IndexDownloader_NewFiltersAsync;
					BitcoinStore.MempoolService.TransactionReceived -= Mempool_TransactionReceived;
					TransactionProcessor.WalletRelevantTransactionProcessed -= TransactionProcessor_WalletRelevantTransactionProcessed;
				}
			}
		}
		finally
		{
			State = WalletState.Stopped;
		}
	}

	private void TransactionProcessor_WalletRelevantTransactionProcessed(object? sender, ProcessedResult e)
	{
		try
		{
			WalletRelevantTransactionProcessed.SafeInvoke(this, e);
			if (e.Transaction.CanBeSpeedUpUsingCpfp())
			{
				CpfpInfoProvider.ScheduleRequest(e.Transaction);
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
		}
	}

	private void Mempool_TransactionReceived(object? sender, SmartTransaction tx)
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
			Logger.LogWarning(ex);
		}
	}

	private void IndexDownloader_NewFiltersAsync(object? sender, FilterModel[] filters)
	{
		NewFiltersProcessed.SafeInvoke(this, filters);
	}

	private async Task LoadWalletStateAsync(CancellationToken cancel)
	{
		// Make sure that the keys are asserted in case of an empty HdPubKeys array.
		KeyManager.GetKeys();

		TransactionProcessor.Process(BitcoinStore.TransactionStore.ConfirmedStore.GetTransactions());

		BitcoinStore.IndexStore.NewFilters += IndexDownloader_NewFiltersAsync;

		// Each time a new batch of filters is downloaded, request a synchronization.
		var lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
		while (BitcoinStore.SmartHeaderChain.HashesLeft > 0)
		{
			cancel.ThrowIfCancellationRequested();
			if (lastHashesLeft == BitcoinStore.SmartHeaderChain.HashesLeft)
			{
				await Task.Delay(100, cancel).ConfigureAwait(false);
				continue;
			}
			lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
		}

		await WalletFilterProcessor.InitialSynchronizationFinished.ConfigureAwait(false);
	}

	private void LoadDummyMempool()
	{
		if (BitcoinStore.TransactionStore.MempoolStore.IsEmpty())
		{
			return;
		}

		// Only clean the mempool if we're fully synchronized.
		if (BitcoinStore.SmartHeaderChain.HashesLeft == 0)
		{
			var txsToProcess = new List<SmartTransaction>();
			foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
			{
				var txid = tx.GetHash();
				if (DateTimeOffset.UtcNow - tx.FirstSeen < TimeSpan.FromDays(ServiceConfiguration.DropUnconfirmedTransactionsAfterDays))
				{
					txsToProcess.Add(tx);
				}
				else
				{
					if (BitcoinStore.TransactionStore.MempoolStore.TryRemove(txid, out _))
					{
						Logger.LogInfo($"Transaction {txid} dropped after {ServiceConfiguration.DropUnconfirmedTransactionsAfterDays} days being unconfirmed.");
					}
				}
			}

			TransactionProcessor.Process(txsToProcess);
		}
		else
		{
			TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());
		}
	}

	public void SetWaitingForInitState()
	{
		if (State != WalletState.Uninitialized)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized}. Current state: {State}.");
		}

		State = WalletState.WaitingForInit;
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

	private void EnsureHeightsAreAtLeastSegWitActivation()
	{
		var startingSegwitHeight = new Height(SmartHeader.GetStartingHeader(Network).Height);
		if (startingSegwitHeight > KeyManager.GetBestHeight())
		{
			KeyManager.SetBestHeight(startingSegwitHeight);
		}
	}
}
