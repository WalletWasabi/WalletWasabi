using System;
using System.IO;
using System.Net;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Tests.XunitConfiguration
{
	public class SharedFixture : IDisposable
	{
		private static string _dataDir = null;
		public static IPEndPoint TorSocks5Endpoint { get; } = new IPEndPoint(IPAddress.Loopback, 9050);

		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Tests"));

				return _dataDir;
			}
		}

		public SharedFixture()
		{
			// Initialize tests...

			Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public void Dispose()
		{
			// Cleanup tests...
		}
	}
}
