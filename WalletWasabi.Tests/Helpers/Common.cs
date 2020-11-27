using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Tor;

namespace WalletWasabi.Tests.Helpers
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

		public static string GetWorkDir([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "")
		{
			return Path.Combine(DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
		}

		public static IEnumerable<TResult> Repeat<TResult>(Func<TResult> action, int count)
		{
			for (int i = 0; i < count; i++)
			{
				yield return action();
			}
		}
	}
}
