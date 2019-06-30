using System.IO;
using System.Net;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Tests
{
	public class Global
	{
		public static Global Instance { get; } = new Global();

		public IPEndPoint TorSocks5Endpoint { get; }

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
	}
}
