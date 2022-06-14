using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent;

public static class Services
{
	public static string DataDir { get; private set; } = null!;

	public static TorSettings TorSettings { get; private set; } = null!;

	public static BitcoinStore BitcoinStore { get; private set; } = null!;

	public static HttpClientFactory HttpClientFactory { get; private set; } = null!;

	public static LegalChecker LegalChecker { get; private set; } = null!;

	public static Config Config { get; private set; } = null!;

	public static WasabiSynchronizer Synchronizer { get; private set; } = null!;

	public static WalletManager WalletManager { get; private set; } = null!;

	public static TransactionBroadcaster TransactionBroadcaster { get; private set; } = null!;

	public static HostedServices HostedServices { get; private set; } = null!;

	public static UiConfig UiConfig { get; private set; } = null!;

	public static SingleInstanceChecker SingleInstanceChecker { get; private set; } = null!;

	public static bool IsInitialized { get; private set; }

	/// <summary>
	/// Initializes global services used by fluent project.
	/// </summary>
	/// <param name="global">The global instance.</param>
	/// <param name="singleInstanceChecker">The singleInstanceChecker instance.</param>
	public static void Initialize(Global global, SingleInstanceChecker singleInstanceChecker)
	{
		Guard.NotNull(nameof(global.DataDir), global.DataDir);
		Guard.NotNull(nameof(global.TorSettings), global.TorSettings);
		Guard.NotNull(nameof(global.BitcoinStore), global.BitcoinStore);
		Guard.NotNull(nameof(global.HttpClientFactory), global.HttpClientFactory);
		Guard.NotNull(nameof(global.LegalChecker), global.LegalChecker);
		Guard.NotNull(nameof(global.Config), global.Config);
		Guard.NotNull(nameof(global.WalletManager), global.WalletManager);
		Guard.NotNull(nameof(global.TransactionBroadcaster), global.TransactionBroadcaster);
		Guard.NotNull(nameof(global.HostedServices), global.HostedServices);
		Guard.NotNull(nameof(global.UiConfig), global.UiConfig);

		DataDir = global.DataDir;
		TorSettings = global.TorSettings;
		BitcoinStore = global.BitcoinStore;
		HttpClientFactory = global.HttpClientFactory;
		LegalChecker = global.LegalChecker;
		Config = global.Config;
		Synchronizer = global.Synchronizer;
		WalletManager = global.WalletManager;
		TransactionBroadcaster = global.TransactionBroadcaster;
		HostedServices = global.HostedServices;
		UiConfig = global.UiConfig;
		SingleInstanceChecker = singleInstanceChecker;

		IsInitialized = true;
	}
}
