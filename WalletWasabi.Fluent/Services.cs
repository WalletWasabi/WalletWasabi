using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Client;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public class Services : IServices
{
	// Temporary solution. It should be removed
	public static Services Instance { get; private set; } = null!;

	private readonly TorSettings _torSettings;
	private readonly FilterStore _filterStore;
	private readonly SmartHeaderChain _smartHeaderChain;
	private readonly AllTransactionStore _transactionStore;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly TransactionBroadcaster _transactionBroadcaster;
	private readonly HostedServices _hostedServices;
	private readonly TerminateService _terminateService;
	private readonly StatusContainer _status;

	private Services(Global global, UiConfig uiConfig, TerminateService terminateService)
	{
		Guard.NotNull(nameof(global.DataDir), global.DataDir);
		Guard.NotNull(nameof(global.TorSettings), global.TorSettings);
		Guard.NotNull(nameof(global.FilterStore), global.FilterStore);
		Guard.NotNull(nameof(global.SmartHeaderChain), global.SmartHeaderChain);
		Guard.NotNull(nameof(global.TransactionStore), global.TransactionStore);
		Guard.NotNull(nameof(global.ExternalSourcesHttpClientFactory), global.ExternalSourcesHttpClientFactory);
		Guard.NotNull(nameof(global.Config), global.Config);
		Guard.NotNull(nameof(global.WalletManager), global.WalletManager);
		Guard.NotNull(nameof(global.TransactionBroadcaster), global.TransactionBroadcaster);
		Guard.NotNull(nameof(global.HostedServices), global.HostedServices);
		Guard.NotNull(nameof(uiConfig), uiConfig);
		Guard.NotNull(nameof(terminateService), terminateService);

		_torSettings = global.TorSettings;
		_filterStore = global.FilterStore;
		_smartHeaderChain = global.SmartHeaderChain;
		_transactionStore = global.TransactionStore;
		_httpClientFactory = global.ExternalSourcesHttpClientFactory;
		_transactionBroadcaster = global.TransactionBroadcaster;
		_hostedServices = global.HostedServices;
		_terminateService = terminateService;
		_status = global.Status;
		Scheme = global.Scheme;
		DataDir = global.DataDir;
		PersistentConfig = global.Config.PersistentConfig;
		WalletManager = global.WalletManager;
		UiConfig = uiConfig;
		Config = global.Config;
		EventBus = global.EventBus;
	}

	public string DataDir { get; }
	public string PersistentConfigFilePath => Path.Combine(DataDir, PersistentConfig.GetConfigFileName());
	public PersistentConfig PersistentConfig { get; }
	public WalletManager WalletManager { get; }
	public UiConfig UiConfig { get; }
	public Config Config { get; }
	public EventBus EventBus { get; }
	public Client.Scheme Scheme { get; }

	// Chain info
	public uint GetTipHeight() => _smartHeaderChain.TipHeight;
	public uint GetServerTipHeight() => _smartHeaderChain.ServerTipHeight;
	public int GetHashesLeft() => _smartHeaderChain.HashesLeft;
	public SmartHeader? GetTip() => _smartHeaderChain.Tip;

	// Filters info
	public uint? GetMinimumBlockHeight() => _filterStore.GetMinimumBlockHeight();

	// Transactions info
	public IEnumerable<LabelsArray> GetTransactionLabels() => _transactionStore.GetLabels();
	public bool TryGetTransaction(uint256 hash, [NotNullWhen(true)] out SmartTransaction? tx) => _transactionStore.TryGetTransaction(hash, out tx);

	// WalletManager info
	public Network GetNetwork() => WalletManager.Network;
	public IEnumerable<Wallet> GetWallets() => WalletManager.GetWallets();
	public bool HasWallet() => WalletManager.HasWallet();
	public Wallet GetWalletByName(string walletName) => WalletManager.GetWalletByName(walletName);
	public void RenameWallet(Wallet wallet, string newWalletName) => WalletManager.RenameWallet(wallet, newWalletName);
	public string GetWalletsDir() => WalletManager.WalletDirectories.WalletsDir;
	public string GetNextWalletName(string prefix) => WalletManager.WalletDirectories.GetNextWalletName(prefix);
	public string GetWalletFilePath(string walletName) => WalletManager.WalletDirectories.GetWalletFilePaths(walletName);
	public (ErrorSeverity Severity, string Message)? ValidateWalletName(string walletName) => WalletManager.ValidateWalletName(walletName);
	public Task StartWalletAsync(Wallet wallet) => WalletManager.StartWalletAsync(wallet);
	public void AddWallet(KeyManager keyManager) => WalletManager.AddWallet(keyManager);

	// Tor info
	public string GetTorLogFilePath() => _torSettings.LogFilePath;
	public TorMode GetUseTor() => Config.UseTor;

	// ExchangeRate info
	public decimal GetUsdExchangeRate() => _status.UsdExchangeRate;

	// UI Config
	public bool GetHideOnClose() => UiConfig.HideOnClose;
	public double? GetWindowWidth() => UiConfig.WindowWidth;
	public double? GetWindowHeight() => UiConfig.WindowHeight;
	public void SetWindowWidth(double? width) => UiConfig.WindowWidth = width;
	public void SetWindowHeight(double? height) => UiConfig.WindowHeight = height;
	public string? GetLastSelectedWallet() => UiConfig.LastSelectedWallet;
	public void SetLastSelectedWallet(string? walletName) => UiConfig.LastSelectedWallet = walletName;
	public bool GetPrivacyMode() => UiConfig.PrivacyMode;
	public bool GetAutocopy() => UiConfig.Autocopy;
	public bool GetAutoPaste() => UiConfig.AutoPaste;
	public bool GetSendAmountConversionReversed() => UiConfig.SendAmountConversionReversed;
	public void SetSendAmountConversionReversed(bool value) => UiConfig.SendAmountConversionReversed = value;
	public int GetFeeTarget() => UiConfig.FeeTarget;
	public void SetFeeTarget(int value) => UiConfig.FeeTarget = value;

	// Temporary solution
	public T? GetHostedService<T>() where T : class, Microsoft.Extensions.Hosting.IHostedService => _hostedServices.GetOrDefault<T>();

	// Transaction
	public Task SendTransactionAsync(SmartTransaction transaction) => _transactionBroadcaster.SendTransactionAsync(transaction);

	// HttpClientFactory wrapper functions
	public HttpClient CreateHttpClient(string name) => _httpClientFactory.CreateClient(name);

	// TerminateService wrapper functions
	public bool IsForcefulTerminationRequested() => _terminateService.ForcefulTerminationRequestedTask.IsCompletedSuccessfully;

	public static Services Create(Global global, UiConfig uiConfig, TerminateService terminateService)
	{
		Instance = new Services(global, uiConfig, terminateService);
		return Instance;
	}
}
