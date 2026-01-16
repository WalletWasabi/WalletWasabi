using System.IO;
using System.Net.Http;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Daemon;
using WalletWasabi.Daemon.Configuration;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent;

public static class Services
{
	public static string DataDir { get; private set; } = null!;

	public static TorSettings TorSettings { get; private set; } = null!;

	public static BitcoinStore BitcoinStore { get; private set; } = null!;

	public static SmartHeaderChain SmartHeaderChain => BitcoinStore.SmartHeaderChain;

	public static IHttpClientFactory HttpClientFactory { get; private set; } = null!;

	public static string PersistentConfigFilePath => Path.Combine(DataDir, PersistentConfig.GetConfigFileName());

	public static PersistentConfig PersistentConfig { get; private set; } = null!;

	public static WalletManager WalletManager { get; private set; } = null!;

	public static TransactionBroadcaster TransactionBroadcaster { get; private set; } = null!;

	public static HostedServices HostedServices { get; private set; } = null!;

	public static UiConfig UiConfig { get; private set; } = null!;

	public static SingleInstanceChecker SingleInstanceChecker { get; private set; } = null!;

	public static TerminateService TerminateService { get; private set; } = null!;

	public static Config Config { get; set; } = null!;
	public static StatusContainer Status { get; set; }

	public static EventBus EventBus { get;  set; } = null!;

	public static Daemon.Scheme Scheme { get; set; }
	public static bool IsInitialized { get; private set; }

	/// <summary>
	/// Initializes global services used by fluent project.
	/// </summary>
	public static void Initialize(Global global, UiConfig uiConfig, SingleInstanceChecker singleInstanceChecker, TerminateService terminateService)
	{
		Guard.NotNull(nameof(global.DataDir), global.DataDir);
		Guard.NotNull(nameof(global.TorSettings), global.TorSettings);
		Guard.NotNull(nameof(global.BitcoinStore), global.BitcoinStore);
		Guard.NotNull(nameof(global.ExternalSourcesHttpClientFactory), global.ExternalSourcesHttpClientFactory);
		Guard.NotNull(nameof(global.Config), global.Config);
		Guard.NotNull(nameof(global.WalletManager), global.WalletManager);
		Guard.NotNull(nameof(global.TransactionBroadcaster), global.TransactionBroadcaster);
		Guard.NotNull(nameof(global.HostedServices), global.HostedServices);
		Guard.NotNull(nameof(uiConfig), uiConfig);
		Guard.NotNull(nameof(terminateService), terminateService);

		DataDir = global.DataDir;
		TorSettings = global.TorSettings;
		BitcoinStore = global.BitcoinStore;
		HttpClientFactory = global.ExternalSourcesHttpClientFactory;
		PersistentConfig = global.Config.PersistentConfig;
		WalletManager = global.WalletManager;
		TransactionBroadcaster = global.TransactionBroadcaster;
		HostedServices = global.HostedServices;
		UiConfig = uiConfig;
		SingleInstanceChecker = singleInstanceChecker;
		Config = global.Config;
		TerminateService = terminateService;
		EventBus = global.EventBus;
		Status = global.Status;
		Scheme = global.Scheme;

		IsInitialized = true;
	}
}
