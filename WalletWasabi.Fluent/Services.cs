using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Gui;
using WalletWasabi.Services;
using WalletWasabi.Stores;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.Fluent
{
	public static class Services
	{
		public static string DataDir { get; private set; }
		public static TorSettings TorSettings { get; private set; }
		public static BitcoinStore BitcoinStore { get; set; }
		public static HttpClientFactory ExternalHttpClientFactory { get; private set; }
		public static LegalChecker LegalChecker { get; private set; }
		public static Config Config { get; private set; }
		public static WasabiSynchronizer Synchronizer { get; private set; }
		public static WalletManager WalletManager { get; private set; }
		public static TransactionBroadcaster TransactionBroadcaster { get; private set; }
		public static HostedServices HostedServices { get; private set; }
		public static UiConfig UiConfig { get; private set; }
		public static bool IsInitialized { get; private set; }

		/// <summary>
		/// Initializes global services used by fluent project.
		/// </summary>
		/// <param name="global">The global instance.</param>
		public static void Initialize(Global global)
		{
			DataDir = global.DataDir;
			TorSettings = global.TorSettings;
			BitcoinStore = global.BitcoinStore;
			ExternalHttpClientFactory = global.ExternalHttpClientFactory;
			LegalChecker = global.LegalChecker;
			Config = global.Config;
			Synchronizer = global.Synchronizer;
			WalletManager = global.WalletManager;
			TransactionBroadcaster = global.TransactionBroadcaster;
			HostedServices = global.HostedServices;
			UiConfig = global.UiConfig;
			IsInitialized = true;
		}
	}
}