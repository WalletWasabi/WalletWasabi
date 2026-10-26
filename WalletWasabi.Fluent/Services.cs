using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public class Services : IServices
{
	// Temporary solution. It should be removed
	public static Services Instance { get; private set; } = null!;

	private readonly Global _global;
	private readonly TorSettings _torSettings;
	private readonly FilterStore _filterStore;
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
		Guard.NotNull(nameof(global.FilterStore), global.FilterStore);
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
		_filterStore = global.FilterStore;
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
	public uint? GetMinimumBlockHeight() => _filterStore.GetMinimumBlockHeight();

	// Transactions info
	public IEnumerable<LabelsArray> GetTransactionLabels() => _transactionStore.GetLabels();
	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx) => _transactionStore.TryGetTransaction(hash, out tx);

	// WalletManager info
	public Network GetNetwork() => _walletManager.Network;
	public IEnumerable<Wallet> GetWallets() => _walletManager.GetWallets();
	public bool HasWallet() => _walletManager.HasWallet();
	public Wallet GetWalletByName(string walletName) => _walletManager.GetWalletByName(walletName);
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

	// Tor info
	public string GetTorLogFilePath() => _torSettings.LogFilePath;
	public TorMode GetUseTor() => _config.UseTor;

	// ExchangeRate info
	public decimal GetUsdExchangeRate() => _status.UsdExchangeRate;

	// CoinJoin
	private CoinJoinManager? CoinJoinManager => _hostedServices.GetOrDefault<CoinJoinManager>();
	private Wallet? GetWalletById(WalletId walletId) => _walletManager.GetWallets().FirstOrDefault(w => w.WalletId == walletId);
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
		Instance = new Services(global, terminateService);
		return Instance;
	}
}
