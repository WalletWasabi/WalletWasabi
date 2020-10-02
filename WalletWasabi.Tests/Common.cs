using NBitcoin;
using System.IO;
using System.Net;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Tor;

namespace WalletWasabi.Tests
{
	public static class Common
	{
		static Common()
		{
			Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public static EndPoint TorSocks5Endpoint => new IPEndPoint(IPAddress.Loopback, 9050);

		public static TorSettings TorSettings
		{
			get
			{
				string torLogsFile = Path.Combine(DataDir, "TorLogs.txt");
				string torDistributionFolder = Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");
				var torSettings = new TorSettings(DataDir, torLogsFile, torDistributionFolder);
				return torSettings;
			}
		}

		public static string DataDir => EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

		public static SmartTransaction GetRandomSmartTransaction()
		{
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(Money.Coins(1), new Key());
			var stx = new SmartTransaction(tx, Height.Mempool);
			return stx;
		}
	}
}
