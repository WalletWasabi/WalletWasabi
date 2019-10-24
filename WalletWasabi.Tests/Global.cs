using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Transactions;

namespace WalletWasabi.Tests
{
	public class Global
	{
		public static Global Instance { get; } = new Global();

		public EndPoint TorSocks5Endpoint { get; }

		public string DataDir { get; }

		public string TorLogsFile { get; }

		public Global()
		{
			TorSocks5Endpoint = new IPEndPoint(IPAddress.Loopback, 9050);

			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));
			TorLogsFile = Path.Combine(DataDir, "TorLogs.txt");

			Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public static SmartTransaction GenerateRandomSmartTransaction()
		{
			var tx = Transaction.Create(Network.Main);
			tx.Outputs.Add(Money.Coins(1), new Key());
			return new SmartTransaction(tx, Height.Mempool);
		}
	}
}
