using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Daemon;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Tor.StatusChecker;
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

	public static PersistentConfig PersistentConfig { get; private set; } = null!;

	public static WasabiSynchronizer Synchronizer { get; private set; } = null!;

	public static WalletManager WalletManager { get; private set; } = null!;

	public static TransactionBroadcaster TransactionBroadcaster { get; private set; } = null!;

	public static HostedServices HostedServices { get; private set; } = null!;

	public static UiConfig UiConfig { get; private set; } = null!;

	public static SingleInstanceChecker SingleInstanceChecker { get; private set; } = null!;

	public static TorStatusChecker TorStatusChecker { get; private set; } = null!;
	public static UpdateManager? UpdateManager { get; private set; }
	public static Task TerminationRequestedTask { get; private set; } = null!;

	public static bool IsInitialized { get; private set; }

	/// <summary>
	/// Initializes global services used by fluent project.
	/// </summary>
	/// <param name="global">The global instance.</param>
	/// <param name="singleInstanceChecker">The singleInstanceChecker instance.</param>
	public static void Initialize(Global global, UiConfig uiConfig, SingleInstanceChecker singleInstanceChecker, Task terminationRequestedTask)
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
		Guard.NotNull(nameof(global.TorStatusChecker), global.TorStatusChecker);
		Guard.NotNull(nameof(global.UpdateManager), global.UpdateManager);
		Guard.NotNull(nameof(uiConfig), uiConfig);

		DataDir = global.DataDir;
		TorSettings = global.TorSettings;
		BitcoinStore = global.BitcoinStore;
		HttpClientFactory = global.HttpClientFactory;
		LegalChecker = global.LegalChecker;
		PersistentConfig = global.Config.PersistentConfig;
		Synchronizer = global.Synchronizer;
		WalletManager = global.WalletManager;
		TransactionBroadcaster = global.TransactionBroadcaster;
		HostedServices = global.HostedServices;
		UiConfig = uiConfig;
		SingleInstanceChecker = singleInstanceChecker;
		TorStatusChecker = global.TorStatusChecker;
		UpdateManager = global.UpdateManager;
		TerminationRequestedTask = terminationRequestedTask;

		IsInitialized = true;
	}
}
