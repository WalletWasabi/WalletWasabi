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
		public static string DataDir { get; set; }
		public static TorSettings TorSettings { get; set; }
		public static BitcoinStore BitcoinStore { get; set; }
		public static HttpClientFactory ExternalHttpClientFactory { get; set; }
		public static LegalChecker LegalChecker { get; set; }
		public static Config Config { get; set; }
		public static WasabiSynchronizer Synchronizer { get; set; }
		public static WalletManager WalletManager { get; set; }
		public static TransactionBroadcaster TransactionBroadcaster { get; set; }

		public static HostedServices HostedServices { get; set; }
		public static UiConfig UiConfig { get; set; }
		public static bool IsInitialized { get; set; }
	}
}