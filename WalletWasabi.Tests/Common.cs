using NBitcoin;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
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
		public static string TorLogsFile => Path.Combine(DataDir, "TorLogs.txt");
		public static string TorDistributionFolder => Path.Combine(EnvironmentHelpers.GetFullBaseDirectory(), "TorDaemons");
		public static TorSettings TorSettings => new TorSettings(DataDir, TorLogsFile, TorDistributionFolder);

		public static string DataDir => EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

		public static SmartTransaction GetRandomSmartTransaction()
		{
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(Money.Coins(1), new Key());
			var stx = new SmartTransaction(tx, Height.Mempool);
			return stx;
		}

		public static string GetWorkDir([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			return Path.Combine(DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
		}
	}
}
