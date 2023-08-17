using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.TransactionProcessing;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WebClients.PayJoin;

namespace WalletWasabi.Wallets;

public class Wallet : BackgroundService, IWallet
{
	private volatile WalletState _state;

	public Wallet(string dataDir, Network network, string filePath) : this(dataDir, network, KeyManager.FromFile(filePath))
	{
	}

	public Wallet(string dataDir, Network network, KeyManager keyManager)
	{
		Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
		Network = Guard.NotNull(nameof(network), network);
		KeyManager = Guard.NotNull(nameof(keyManager), keyManager);

		RuntimeParams.SetDataDir(dataDir);

		if (!KeyManager.IsWatchOnly)
		{
			KeyChain = new KeyChain(KeyManager, Kitchen);
		}

		DestinationProvider = new InternalDestinationProvider(KeyManager);
	}

	public event EventHandler<ProcessedResult>? WalletRelevantTransactionProcessed;
	public event EventHandler<IEnumerable<FilterModel>>? NewFiltersProcessed;

	public event EventHandler<WalletState>? StateChanged;

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

	public BitcoinStore BitcoinStore { get; private set; }
	public KeyManager KeyManager { get; }
	public WasabiSynchronizer Synchronizer { get; private set; }
	public ServiceConfiguration ServiceConfiguration { get; private set; }
	public string WalletName => KeyManager.WalletName;

	/// <summary>Unspent Transaction Outputs</summary>
	public ICoinsView Coins { get; private set; }

	public bool RedCoinIsolation => KeyManager.RedCoinIsolation;

	public Network Network { get; }
	public TransactionProcessor TransactionProcessor { get; private set; }

	public HybridFeeProvider FeeProvider { get; private set; }
	
	public WalletFilterProcessor WalletFilterProcessor { get; private set; }
	public FilterModel? LastProcessedFilter => WalletFilterProcessor?.LastProcessedFilter;
	public IBlockProvider BlockProvider { get; private set; }

	public bool IsLoggedIn { get; private set; }

	public Kitchen Kitchen { get; } = new();

	public IKeyChain? KeyChain { get; }

	public IDestinationProvider DestinationProvider { get; }

	public int AnonScoreTarget => KeyManager.AnonScoreTarget;
	public bool ConsolidationMode => false;

	public bool IsMixable =>
		State == WalletState.Started // Only running wallets
		&& !KeyManager.IsWatchOnly // that are not watch-only wallets
		&& Kitchen.HasIngredients;

	public TimeSpan FeeRateMedianTimeFrame => TimeSpan.FromHours(KeyManager.FeeRateMedianTimeFrameHours);

	public bool IsUnderPlebStop => Coins.TotalAmount() <= KeyManager.PlebStopThreshold;

	public ICoinsView GetAllCoins() => ((CoinsRegistry)Coins).AsAllCoinsView();

	public Task<bool> IsWalletPrivateAsync() => Task.FromResult(IsWalletPrivate());

	public bool IsWalletPrivate() => GetPrivacyPercentage(new CoinsView(Coins), AnonScoreTarget) >= 1;

	public Task<IEnumerable<SmartTransaction>> GetTransactionsAsync() => Task.FromResult(GetTransactions());

	public Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync() => Task.FromResult(GetCoinjoinCoinCandidates());

	public IEnumerable<SmartCoin> GetCoinjoinCoinCandidates() => Coins;

	public IEnumerable<SmartTransaction> GetTransactions()
	{
		var walletTransactions = new List<SmartTransaction>();
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

	public HdPubKey GetNextReceiveAddress(IEnumerable<string> destinationLabels)
	{
		return KeyManager.GetNextReceiveKey(new LabelsArray(destinationLabels));
	}

	private double GetPrivacyPercentage(CoinsView coins, int privateThreshold)
	{
		var privateAmount = coins.FilterBy(x => x.IsPrivate(privateThreshold)).TotalAmount();
		var normalAmount = coins.FilterBy(x => !x.IsPrivate(privateThreshold)).TotalAmount();

		var privateDecimalAmount = privateAmount.ToDecimal(MoneyUnit.BTC);
		var normalDecimalAmount = normalAmount.ToDecimal(MoneyUnit.BTC);
		var totalDecimalAmount = privateDecimalAmount + normalDecimalAmount;

		var pcPrivate = totalDecimalAmount == 0M ? 1d : (double)(privateDecimalAmount / totalDecimalAmount);
		return pcPrivate;
	}

	public bool TryLogin(string password, out string? compatibilityPasswordUsed)
	{
		compatibilityPasswordUsed = null;

		if (KeyManager.IsWatchOnly)
		{
			IsLoggedIn = true;
			Kitchen.Cook("");
		}
		else if (PasswordHelper.TryPassword(KeyManager, password, out compatibilityPasswordUsed))
		{
			IsLoggedIn = true;
			Kitchen.Cook(compatibilityPasswordUsed ?? Guard.Correct(password));
		}

		return IsLoggedIn;
	}

	public void Logout()
	{
		Kitchen.CleanUp();
		IsLoggedIn = false;
	}

	public void RegisterServices(
		BitcoinStore bitcoinStore,
		WasabiSynchronizer syncer,
		ServiceConfiguration serviceConfiguration,
		HybridFeeProvider feeProvider,
		IBlockProvider blockProvider)
	{
		if (State > WalletState.WaitingForInit)
		{
			throw new InvalidOperationException($"{nameof(State)} must be {WalletState.Uninitialized} or {WalletState.WaitingForInit}. Current state: {State}.");
		}

		try
		{
			BitcoinStore = Guard.NotNull(nameof(bitcoinStore), bitcoinStore);
			Synchronizer = Guard.NotNull(nameof(syncer), syncer);
			ServiceConfiguration = Guard.NotNull(nameof(serviceConfiguration), serviceConfiguration);
			FeeProvider = Guard.NotNull(nameof(feeProvider), feeProvider);

			TransactionProcessor = new TransactionProcessor(BitcoinStore.TransactionStore, BitcoinStore.MempoolService, KeyManager, ServiceConfiguration.DustThreshold);
			Coins = TransactionProcessor.Coins;

			TransactionProcessor.WalletRelevantTransactionProcessed += TransactionProcessor_WalletRelevantTransactionProcessed;
			BitcoinStore.MempoolService.TransactionReceived += Mempool_TransactionReceived;

			BlockProvider = blockProvider;

			WalletFilterProcessor = new WalletFilterProcessor(KeyManager, BitcoinStore, TransactionProcessor, BlockProvider);
			
			BitcoinStore.IndexStore.NewFilters += IndexDownloader_NewFiltersAsync;
			
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

			if (!Synchronizer.IsRunning)
			{
				throw new NotSupportedException($"{nameof(Synchronizer)} is not running.");
			}

			using (BenchmarkLogger.Measure())
			{
				await RuntimeParams.LoadAsync().ConfigureAwait(false);

				await WalletFilterProcessor.StartAsync(cancel).ConfigureAwait(false);

				await LoadWalletStateAsync(cancel).ConfigureAwait(false);
				await LoadDummyMempoolAsync().ConfigureAwait(false);
				LoadExcludedCoins();
			}

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
		// Perform final synchronization in the background.
		if (KeyManager.UseTurboSync)
		{
			await PerformSynchronizationAsync(SyncType.NonTurbo, stoppingToken).ConfigureAwait(false);
		}
		Logger.LogInfo($"Wallet '{WalletName}' is fully synchronized.");
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

				if (prevState >= WalletState.Initialized)
				{
					UnregisterNewFiltersEvent();
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
			WalletRelevantTransactionProcessed?.Invoke(this, e);
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

	private async void IndexDownloader_NewFiltersAsync(object? sender, IEnumerable<FilterModel> filters)
	{
		try
		{ 
			var filterModels = filters as FilterModel[] ?? filters.ToArray();

			if (KeyManager.UseTurboSync)
			{
				await WalletFilterProcessor.ProcessAsync(new List<SyncType> { SyncType.Turbo, SyncType.NonTurbo }, CancellationToken.None).ConfigureAwait(false);
			}
			else
			{
				await WalletFilterProcessor.ProcessAsync(SyncType.Complete, CancellationToken.None).ConfigureAwait(false);
			}

			NewFiltersProcessed?.Invoke(this, filterModels);
			await Task.Delay(100).ConfigureAwait(false);

			if (Synchronizer is null || BitcoinStore?.SmartHeaderChain is null)
			{
				return;
			}

			// Make sure fully synced and this filter is the latest filter.
			if (BitcoinStore.SmartHeaderChain.HashesLeft != 0 || BitcoinStore.SmartHeaderChain.TipHash != filterModels.Last().Header.BlockHash)
			{
				return;
			}

			var task = BitcoinStore.MempoolService.TryPerformMempoolCleanupAsync(Synchronizer.HttpClientFactory);

			if (task is { })
			{
				await task.ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			// Cancellation token kicked in while processing the new filters, don't log anything.
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}

	internal void UnregisterNewFiltersEvent()
	{
		BitcoinStore.IndexStore.NewFilters -= IndexDownloader_NewFiltersAsync;
	}
	
	private async Task LoadWalletStateAsync(CancellationToken cancel)
	{
		KeyManager.AssertNetworkOrClearBlockState(Network);

		// Make sure that the keys are asserted in case of an empty HdPubKeys array.
		KeyManager.GetKeys();

		Height bestKeyManagerHeight = KeyManager.GetBestTurboSyncHeight();

		using (BenchmarkLogger.Measure(LogLevel.Info, "Initial Transaction Processing"))
		{
			TransactionProcessor.Process(BitcoinStore.TransactionStore.ConfirmedStore.GetTransactions().TakeWhile(x => x.Height <= bestKeyManagerHeight));
		}

		var lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
		while (BitcoinStore.SmartHeaderChain.HashesLeft > 0)
		{
			if (lastHashesLeft == BitcoinStore.SmartHeaderChain.HashesLeft)
			{
				continue;
			}
			lastHashesLeft = BitcoinStore.SmartHeaderChain.HashesLeft;
			await PerformSynchronizationAsync(KeyManager.UseTurboSync ? SyncType.Turbo : SyncType.Complete, cancel).ConfigureAwait(false);
		}
		await PerformSynchronizationAsync(KeyManager.UseTurboSync ? SyncType.Turbo : SyncType.Complete, cancel).ConfigureAwait(false);
	}

	public async Task PerformSynchronizationAsync(SyncType syncType, CancellationToken cancellationToken)
	{
		await WalletFilterProcessor.ProcessAsync(syncType, cancellationToken).ConfigureAwait(false);
	}
	private async Task LoadDummyMempoolAsync()
	{
		if (BitcoinStore.TransactionStore.MempoolStore.IsEmpty())
		{
			return;
		}

		// Only clean the mempool if we're fully synchronized.
		if (BitcoinStore.SmartHeaderChain.HashesLeft == 0)
		{
			try
			{
				var client = Synchronizer.HttpClientFactory.SharedWasabiClient;
				var compactness = 10;

				var mempoolHashes = await client.GetMempoolHashesAsync(compactness).ConfigureAwait(false);

				var txsToProcess = new List<SmartTransaction>();
				foreach (var tx in BitcoinStore.TransactionStore.MempoolStore.GetTransactions())
				{
					uint256 hash = tx.GetHash();
					if (mempoolHashes.Contains(hash.ToString()[..compactness]))
					{
						txsToProcess.Add(tx);
						Logger.LogInfo($"'{WalletName}': Transaction was successfully tested against the backend's mempool hashes: {hash}.");
					}
					else
					{
						BitcoinStore.TransactionStore.MempoolStore.TryRemove(tx.GetHash(), out _);
					}
				}

				TransactionProcessor.Process(txsToProcess);
			}
			catch (Exception ex)
			{
				// When there's a connection failure do not clean the transactions, add them to processing.
				TransactionProcessor.Process(BitcoinStore.TransactionStore.MempoolStore.GetTransactions());

				Logger.LogWarning(ex);
			}
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

	public static Wallet CreateAndRegisterServices(Network network, BitcoinStore bitcoinStore, KeyManager keyManager, WasabiSynchronizer synchronizer, string dataDir, ServiceConfiguration serviceConfiguration, HybridFeeProvider feeProvider, IBlockProvider blockProvider)
	{
		var wallet = new Wallet(dataDir, network, keyManager);
		wallet.RegisterServices(bitcoinStore, synchronizer, serviceConfiguration, feeProvider, blockProvider);
		return wallet;
	}

	public void UpdateExcludedCoinFromCoinJoin()
	{
		var excludedOutpoints = Coins.Where(c => c.IsExcludedFromCoinJoin).Select(c => c.Outpoint);
		KeyManager.SetExcludedCoinsFromCoinJoin(excludedOutpoints);
	}

	public void UpdateUsedHdPubKeysLabels(Dictionary<HdPubKey, LabelsArray> hdPubKeysWithLabels)
	{
		if (!hdPubKeysWithLabels.Any())
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
