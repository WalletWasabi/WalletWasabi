using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Blockchain.Transactions.Summary;
using WalletWasabi.Client;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Userfacing;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Storages;
using WalletWasabi.Tor;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public class Services : IServices
{
	private readonly Global _global;
	private readonly TorSettings _torSettings;
	private readonly FilterManager _filterManager;
	private readonly FilterHeaderChain _filterHeaders;
	private readonly AllTransactionStore _transactionStore;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly TransactionBroadcaster _transactionBroadcaster;
	private readonly HostedServices _hostedServices;
	private readonly TerminateService _terminateService;
	private readonly StatusContainer _status;

	private Services(Global global, TerminateService terminateService)
	{
		Guard.NotNull(nameof(global.DataDir), global.DataDir);
		Guard.NotNull(nameof(global.TorSettings), global.TorSettings);
		Guard.NotNull(nameof(global.FilterManager), global.FilterManager);
		Guard.NotNull(nameof(global.FilterHeaders), global.FilterHeaders);
		Guard.NotNull(nameof(global.TransactionStore), global.TransactionStore);
		Guard.NotNull(nameof(global.ExternalSourcesHttpClientFactory), global.ExternalSourcesHttpClientFactory);
		Guard.NotNull(nameof(global.Config), global.Config);
		Guard.NotNull(nameof(global.WalletManager), global.WalletManager);
		Guard.NotNull(nameof(global.TransactionBroadcaster), global.TransactionBroadcaster);
		Guard.NotNull(nameof(global.HostedServices), global.HostedServices);
		Guard.NotNull(nameof(terminateService), terminateService);

		_global = global;
		_torSettings = global.TorSettings;
		_filterManager = global.FilterManager;
		_filterHeaders = global.FilterHeaders;
		_transactionStore = global.TransactionStore;
		_httpClientFactory = global.ExternalSourcesHttpClientFactory;
		_transactionBroadcaster = global.TransactionBroadcaster;
		_hostedServices = global.HostedServices;
		_terminateService = terminateService;
		_status = global.Status;
		_scheme = global.Scheme;
		_dataDir = global.DataDir;
		_persistentConfig = global.Config.PersistentConfig;
		_walletManager = global.WalletManager;
		_config = global.Config;
		_eventBus = global.EventBus;
	}

	private readonly string _dataDir;
	private readonly PersistentConfig _persistentConfig;
	private readonly WalletManager _walletManager;
	private readonly Config _config;
	private readonly EventBus _eventBus;
	private readonly Client.Scheme _scheme;

	public string GetDataDir() => _dataDir;
	public string GetPersistentConfigFilePath() => Path.Combine(_dataDir, _persistentConfig.GetConfigFileName());
	public Client.Scheme GetScheme() => _scheme;

	// Event subscription
	public IObservable<T> SubscribeToEvent<T>() where T : notnull => _eventBus.AsObservable<T>();
	public void PublishEvent<T>(T evt) where T : notnull => _eventBus.Publish(evt);
	public Task WaitForEventAsync<T>(Func<bool> condition) where T : notnull => _eventBus.WaitForEventAsync<T>(condition);

	// Config
	public string GetBitcoinRpcUri() => _config.BitcoinRpcUri;
	public bool GetEnableGpu() => _config.EnableGpu;
	public ApplicationSettingsData GetApplicationSettings() => ToApplicationSettingsData(_persistentConfig);

	public ApplicationSettingsData GetDefaultApplicationSettings(Network network)
	{
		var defaultConfig = network.Name switch
		{
			"Main" => PersistentConfigManager.DefaultMainNetConfig,
			"TestNet" => PersistentConfigManager.DefaultTestNetConfig,
			"RegTest" => PersistentConfigManager.DefaultRegTestConfig,
			_ when network == Bitcoin.Instance.Signet => PersistentConfigManager.DefaultSignetConfig,
			_ => throw new NotSupportedException($"Network '{network}' is not supported.")
		};
		return ToApplicationSettingsData(defaultConfig);
	}

	public bool SaveApplicationSettings(ApplicationSettingsData settings)
	{
		var configFilePath = GetPersistentConfigFilePath();
		var loadedConfig = PersistentConfigManager.LoadFile(configFilePath);
		if (loadedConfig is not PersistentConfig currentConfig)
		{
			throw new NotSupportedException("Only configuration files after v2.5.1 are supported.");
		}

		var newConfig = currentConfig with
		{
			EnableGpu = settings.EnableGpu,
			BitcoinRpcUri = string.IsNullOrWhiteSpace(settings.BitcoinRpcUri) ? "" :
				Uri.TryCreate(settings.BitcoinRpcUri, UriKind.Absolute, out var uri) ? uri.ToString() : currentConfig.BitcoinRpcUri,
			BitcoinRpcCredentialString = settings.BitcoinRpcCredentialString,
			CoordinatorUri = settings.CoordinatorUri,
			Network = settings.Network,
			DustThreshold = Money.Coins(settings.DustThreshold),
			MaxCoinJoinMiningFeeRate = settings.MaxCoinJoinMiningFeeRate,
			AbsoluteMinInputCount = settings.AbsoluteMinInputCount,
			ExchangeRateProvider = settings.ExchangeRateProvider,
			FeeRateEstimationProvider = settings.FeeRateEstimationProvider,
			ExternalTransactionBroadcaster = settings.ExternalTransactionBroadcaster,
			UseTor = settings.UseTor.ToString(),
			TerminateTorOnExit = settings.TerminateTorOnExit,
			DownloadNewVersion = settings.DownloadNewVersion
		};

		PersistentConfigManager.ToFile(configFilePath, newConfig);
		PersistentConfigManager.UpdateNetwork(configFilePath, newConfig.Network);

		return _persistentConfig != newConfig;
	}

	private ApplicationSettingsData ToApplicationSettingsData(PersistentConfig config) => new()
	{
		UseTor = Config.ObjectToTorMode(config.UseTor),
		ExchangeRateProvider = config.ExchangeRateProvider,
		FeeRateEstimationProvider = config.FeeRateEstimationProvider,
		ExternalTransactionBroadcaster = config.ExternalTransactionBroadcaster,
		Network = config.Network,
		BitcoinRpcUri = config.BitcoinRpcUri,
		BitcoinRpcCredentialString = config.BitcoinRpcCredentialString,
		DustThreshold = config.DustThreshold.ToDecimal(MoneyUnit.BTC),
		CoordinatorUri = config.CoordinatorUri,
		MaxCoinJoinMiningFeeRate = config.MaxCoinJoinMiningFeeRate,
		AbsoluteMinInputCount = config.AbsoluteMinInputCount,
		TerminateTorOnExit = config.TerminateTorOnExit,
		DownloadNewVersion = config.DownloadNewVersion,
		EnableGpu = config.EnableGpu,
		ExperimentalFeatures = config.ExperimentalFeatures.ToArray(),
		IsOverridden = _config.IsOverridden
	};

	// Internal access - not part of IServices interface
	internal PersistentConfig PersistentConfig => _persistentConfig;
	internal Config Config => _config;

	// Chain info
	public uint GetTipHeight() => _filterHeaders.TipHeight;
	public uint GetServerTipHeight() => _filterHeaders.ServerTipHeight;
	public int GetHashesLeft() => _filterHeaders.HashesLeft;
	public SmartHeader? GetTip() => _filterHeaders.Tip;
	public uint GetBlockHeadersTipHeight() => _global.GetBlockHeadersTipHeight();
	public int GetPeerCount() => _global.GetPeerCount();

	// Filters info
	public uint? GetMinimumBlockHeight() => _filterManager.GetMinimumBlockHeight();

	// Transactions info
	public IEnumerable<LabelsArray> GetTransactionLabels() => _transactionStore.GetLabels();
	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx) => _transactionStore.TryGetTransaction(hash, out tx);

	// WalletManager info
	public Network GetNetwork() => _walletManager.Network;

	// Helper to get wallet by ID (internal use only - not exposed via IServices)
	private Wallet? GetWalletById(WalletId walletId) => _walletManager.GetWallets().FirstOrDefault(w => w.WalletId == walletId);

	// Internal method for WalletRepository - returns actual Wallet objects
	// This is temporary until WalletModel is fully refactored to use IServices
	internal IEnumerable<Wallet> GetWalletsInternal() => _walletManager.GetWallets();

	// Wallet management
	public IEnumerable<WalletData> GetWallets() => _walletManager.GetWallets().Select(w => new WalletData(
		w.WalletId,
		w.WalletName,
		w.KeyManager.IsHardwareWallet,
		w.KeyManager.IsWatchOnly,
		w.KeyManager.MasterFingerprint,
		w.Network,
		w.KeyManager.AvailableScriptPubKeyTypes
	));
	public bool HasWallet() => _walletManager.HasWallet();
	public WalletId GetWalletIdByName(string walletName) => _walletManager.GetWalletByName(walletName).WalletId;
	public void RenameWallet(string walletName, string newWalletName) => _walletManager.RenameWallet(_walletManager.GetWalletByName(walletName), newWalletName);
	public string GetWalletsDir() => _walletManager.WalletDirectories.WalletsDir;
	public string GetNextWalletName(string prefix) => _walletManager.WalletDirectories.GetNextWalletName(prefix);
	public string GetWalletFilePath(string walletName) => _walletManager.WalletDirectories.GetWalletFilePaths(walletName);
	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName) => _walletManager.ValidateWalletName(walletName);
	public Task StartWalletAsync(string walletName) => _walletManager.StartWalletAsync(_walletManager.GetWalletByName(walletName));
	public void AddWallet(KeyManager keyManager) => _walletManager.AddWallet(keyManager);
	public Task<KeyManager> ImportWalletAsync(string walletName, string filePath) => ImportWalletHelper.ImportWalletAsync(_walletManager, walletName, filePath);
	public IObservable<WalletId> SubscribeToWalletAdded() => Observable.FromEventPattern<Wallet>(_walletManager, nameof(WalletManager.WalletAdded)).Select(e => e.EventArgs.WalletId);

	// Wallet state
	public IObservable<bool> SubscribeToWalletLoaded(WalletId walletId) =>
		SubscribeToEvent<WalletLoaded>().Select(_ => GetWalletById(walletId)?.Loaded ?? false);
	public bool IsWalletLoaded(WalletId walletId) => GetWalletById(walletId)?.Loaded ?? false;
	public int GetAnonScoreTarget(WalletId walletId) => GetWalletById(walletId)?.AnonScoreTarget ?? 5;

	// Wallet authentication
	public bool HasPassword(WalletId walletId) => !string.IsNullOrEmpty(GetWalletById(walletId)?.Password);
	public string? GetPassword(WalletId walletId) => GetWalletById(walletId)?.Password;
	public async Task<bool> TryLoginAsync(WalletId walletId, string password)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		return await Task.Run(() => wallet.TryLogin(password, out _)).ConfigureAwait(false);
	}
	public async Task<(bool IsValid, string? CompatibilityPassword)> TryLoginWithCompatibilityAsync(WalletId walletId, string password)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return (false, null);
		var isValid = await Task.Run(() => wallet.TryLogin(password, out var compatPwd)).ConfigureAwait(false);
		// Note: The out parameter from TryLogin needs special handling
		string? compatibilityPassword = null;
		await Task.Run(() => wallet.TryLogin(password, out compatibilityPassword)).ConfigureAwait(false);
		return (isValid, compatibilityPassword);
	}
	public async Task<bool> IsPasswordValidAsync(WalletId walletId, string password)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		return await Task.Run(() => PasswordHelper.TryPassword(wallet.KeyManager, password, out _)).ConfigureAwait(false);
	}
	public void Logout(WalletId walletId) => GetWalletById(walletId)?.Logout();
	public async Task<bool> VerifyRecoveryWordsAsync(WalletId walletId, Mnemonic mnemonic)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		var saltSoup = wallet.Password;
		var recovered = await Task.Run(() => KeyManager.Recover(
			mnemonic,
			saltSoup,
			wallet.Network,
			wallet.KeyManager.SegwitAccountKeyPath,
			null,
			"",
			wallet.KeyManager.MinGapLimit)).ConfigureAwait(false);
		return wallet.KeyManager.SegwitExtPubKey == recovered.SegwitExtPubKey;
	}

	// Wallet balance and coins
	public Money GetWalletBalance(WalletId walletId) => GetWalletById(walletId)?.Coins.TotalAmount() ?? Money.Zero;
	public IEnumerable<SmartCoin> GetWalletCoins(WalletId walletId) => GetWalletById(walletId)?.Coins ?? Enumerable.Empty<SmartCoin>();

	// Wallet labels
	public IEnumerable<(string Label, int Score)> GetLabelsWithRanking(WalletId walletId, Intent intent)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return Enumerable.Empty<(string, int)>();

		var labelPool = new Dictionary<string, int>(); // int: score.

		// Get labels by wallet (internal access)
		var labelsByWalletId = GetWalletsInternal()
			.Select(w =>
			{
				var (changeKeys, receiveKeys) = w.KeyManager.GetKeys().Partition(x => x.IsInternal);
				return new
				{
					WalletId = w.WalletId,
					ChangeLabels = changeKeys.SelectMany(x => x.Labels).ToList(),
					ReceiveLabels = receiveKeys.SelectMany(x => x.Labels).ToList()
				};
			}).ToList();

		// Make recent and receive labels count more for the current wallet.
		var multiplier = 100;
		var currentWalletReceiveLabels = labelsByWalletId.First(x => x.WalletId == walletId).ReceiveLabels;
		for (var i = currentWalletReceiveLabels.Count - 1; i >= 0; i--) // Iterate in reverse order.
		{
			var label = currentWalletReceiveLabels[i];
			var score = (intent == Intent.Receive ? 100 : 1) * multiplier;
			if (!labelPool.TryAdd(label, score))
			{
				labelPool[label] += score;
			}

			if (multiplier > 1)
			{
				multiplier--;
			}
		}

		// Receive addresses should be more dominant.
		foreach (var label in labelsByWalletId.SelectMany(x => x.ReceiveLabels))
		{
			var score = intent == Intent.Receive ? 100 : 1;
			if (!labelPool.TryAdd(label, score))
			{
				labelPool[label] += score;
			}
		}

		// Change addresses shouldn't be much dominant, but should be present.
		foreach (var label in labelsByWalletId.SelectMany(x => x.ChangeLabels))
		{
			var score = 1;
			if (!labelPool.TryAdd(label, score))
			{
				labelPool[label] += score;
			}
		}

		multiplier = 100; // Make recent labels count more.
		foreach (var label in GetTransactionLabels().SelectMany(x => x).Reverse())
		{
			var score = (intent == Intent.Send ? 100 : 1) * multiplier;
			if (!labelPool.TryAdd(label, score))
			{
				labelPool[label] += score;
			}

			if (multiplier > 1)
			{
				multiplier--;
			}
		}

		var unwantedLabelSuggestions = new[]
		{
			"test", // Often people use the string "test" as a label. It obviously cannot be a real label, just a test label.
			"zerolink mixed coin", // Obsoleted autogenerated label from old WW1 versions.
			"zerolink change", // Obsoleted autogenerated label from old WW1 versions.
			"zerolink dequeued change" // Obsoleted autogenerated label from old WW1 versions.
		};

		var labels = labelPool
			.Where(x =>
				!unwantedLabelSuggestions.Any(y => y.Equals(x.Key, StringComparison.OrdinalIgnoreCase))
				&& !x.Key.StartsWith("change of (", StringComparison.OrdinalIgnoreCase)); // An obsoleted autogenerated label pattern was from old WW1 versions starting with "change of (".

		var mostUsedLabels = labels
			.GroupBy(x => x.Key)
			.Select(x => new
			{
				Label = x.Key,
				Score = x.Sum(y => y.Value)
			})
			.OrderByDescending(x => x.Score)
			.ToList();

		return mostUsedLabels.Select(x => (x.Label, x.Score)).ToList();
	}

	// Wallet addresses
	public IEnumerable<HdPubKey> GetUnusedKeys(WalletId walletId) =>
		GetWalletById(walletId)?.KeyManager.GetKeys(x => x is { IsInternal: false, KeyState: KeyState.Clean, Labels.Count: > 0 }) ?? Enumerable.Empty<HdPubKey>();
	public HdPubKey GetNextReceiveAddress(WalletId walletId, LabelsArray labels, ScriptPubKeyType scriptType)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) throw new InvalidOperationException($"Wallet not found: {walletId}");
		return wallet.GetNextReceiveAddress(labels, scriptType);
	}
	public void SetKeyState(WalletId walletId, KeyState state, HdPubKey key)
	{
		var wallet = GetWalletById(walletId);
		wallet?.KeyManager.SetKeyState(state, key);
	}
	public void SaveKeyManager(WalletId walletId) => GetWalletById(walletId)?.KeyManager.ToFile();

	// Wallet transactions
	public async Task<IEnumerable<TransactionSummary>> BuildHistorySummaryAsync(WalletId walletId, CancellationToken cancellationToken)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return Enumerable.Empty<TransactionSummary>();
		return await wallet.BuildHistorySummaryAsync(sortForUi: true, cancellationToken: cancellationToken).ConfigureAwait(false);
	}
	public bool TryGetWalletTransaction(WalletId walletId, uint256 txId, [NotNullWhen(true)] out SmartTransaction? tx)
	{
		tx = null;
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		return wallet.TransactionStore.TryGetTransaction(txId, out tx);
	}
	public async Task<TimeSpan?> EstimateConfirmationTimeAsync(WalletId walletId, uint256 txId, CancellationToken cancellationToken)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return null;
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) return null;
		return await TransactionFeeHelper.EstimateConfirmationTimeAsync(wallet.FeeRateEstimations, wallet.Network, tx, wallet.CpfpInfoProvider, cancellationToken).ConfigureAwait(false);
	}
	public async Task<BuildTransactionResult> SpeedUpTransactionAsync(WalletId walletId, uint256 txId, CancellationToken cancellationToken)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) throw new InvalidOperationException($"Wallet not found: {walletId}");
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) throw new InvalidOperationException($"Transaction not found: {txId}");
		return await wallet.SpeedUpTransactionAsync(tx, null, cancellationToken).ConfigureAwait(false);
	}
	public BuildTransactionResult CancelTransaction(WalletId walletId, uint256 txId)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) throw new InvalidOperationException($"Wallet not found: {walletId}");
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) throw new InvalidOperationException($"Transaction not found: {txId}");
		return wallet.CancelTransaction(tx);
	}
	public void UpdateTransactionLabels(WalletId walletId, Dictionary<HdPubKey, LabelsArray> hdPubKeysWithNewLabels)
	{
		var wallet = GetWalletById(walletId);
		wallet?.UpdateUsedHdPubKeysLabels(hdPubKeysWithNewLabels);
	}
	public IEnumerable<Output> GetTransactionOutputs(WalletId walletId, uint256 txId)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return Enumerable.Empty<Output>();
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) return Enumerable.Empty<Output>();
		return tx.GetOutputs(wallet.Network);
	}
	public bool HasFeeRateEstimations(WalletId walletId) => GetWalletById(walletId)?.FeeRateEstimations is not null;
	public bool IsTransactionCancellable(WalletId walletId, uint256 txId)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) return false;
		return tx.IsCancellable(wallet.KeyManager);
	}
	public bool IsTransactionSpeedupable(WalletId walletId, uint256 txId)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return false;
		if (!wallet.TransactionStore.TryGetTransaction(txId, out var tx)) return false;
		return tx.IsSpeedupable(wallet.KeyManager);
	}
	public CoinjoinCosts? GetCoinjoinCosts(WalletId walletId, uint256 txId)
	{
		var wallet = GetWalletById(walletId);
		if (wallet is null) return null;
		return wallet.KeyManager.CoinjoinCosts.GetValueOrDefault(txId);
	}

	// Tor info
	public string GetTorLogFilePath() => _torSettings.LogFilePath;
	public TorMode GetUseTor() => _config.UseTor;

	// ExchangeRate info
	public decimal GetUsdExchangeRate() => _status.UsdExchangeRate;

	// CoinJoin
	private CoinJoinManager? CoinJoinManager => _hostedServices.GetOrDefault<CoinJoinManager>();
	public bool IsCoinJoinEnabled() => CoinJoinManager is not null;
	public CoinJoinClientState GetHighestCoinJoinClientState() => CoinJoinManager?.HighestCoinJoinClientState ?? CoinJoinClientState.Idle;
	public ImmutableList<SmartCoin> GetCoinsInCriticalPhase(WalletId walletId) => CoinJoinManager?.CoinsInCriticalPhase.GetValueOrDefault(walletId) ?? [];
	public async Task RestartAbortedCoinjoinsAsync()
	{
		if (CoinJoinManager is { } cjm)
		{
			await cjm.RestartAbortedCoinjoinsAsync().ConfigureAwait(false);
		}
	}
	public async Task SignalToStopCoinjoinsAsync()
	{
		if (CoinJoinManager is { } cjm)
		{
			await cjm.SignalToStopCoinjoinsAsync().ConfigureAwait(false);
		}
	}
	public void WalletEnteredSendWorkflow(WalletId walletId) => CoinJoinManager?.WalletEnteredSendWorkflow(walletId);
	public void WalletLeftSendWorkflow(WalletId walletId)
	{
		if (CoinJoinManager is { } cjm && GetWalletById(walletId) is { } wallet)
		{
			cjm.WalletLeftSendWorkflow(wallet);
		}
	}
	public async Task WalletEnteredSendingAsync(WalletId walletId)
	{
		if (CoinJoinManager is { } cjm && GetWalletById(walletId) is { } wallet)
		{
			await cjm.WalletEnteredSendingAsync(wallet).ConfigureAwait(false);
		}
	}
	public IObservable<StatusChangedEventArgs> SubscribeToCoinJoinStatusChanged()
	{
		if (CoinJoinManager is { } cjm)
		{
			return Observable.FromEventPattern<StatusChangedEventArgs>(cjm, nameof(CoinJoinManager.StatusChanged))
				.Select(e => e.EventArgs);
		}
		return Observable.Empty<StatusChangedEventArgs>();
	}
	public void RequestCoinJoinStart(WalletId walletId, WalletId outputWalletId, bool stopWhenAllMixed, bool overridePlebStop)
	{
		if (CoinJoinManager is { } cjm && GetWalletById(walletId) is { } wallet && GetWalletById(outputWalletId) is { } outputWallet)
		{
			cjm.RequestCoinJoinStart(wallet, outputWallet, stopWhenAllMixed, overridePlebStop);
		}
	}
	public void RequestCoinJoinStop(WalletId walletId)
	{
		if (CoinJoinManager is { } cjm && GetWalletById(walletId) is { } wallet)
		{
			cjm.RequestCoinJoinStop(wallet);
		}
	}

	// Transaction
	public Task SendTransactionAsync(SmartTransaction transaction) => _transactionBroadcaster.SendTransactionAsync(transaction);

	// HttpClientFactory wrapper functions
	public HttpClient CreateHttpClient(string name) => _httpClientFactory.CreateClient(name);

	// TerminateService wrapper functions
	public bool IsForcefulTerminationRequested() => _terminateService.ForcefulTerminationRequestedTask.IsCompletedSuccessfully;

	public static Services Create(Global global, TerminateService terminateService)
	{
		return new Services(global, terminateService);
	}
}
