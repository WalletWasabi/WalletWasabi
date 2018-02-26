using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MagicalCryptoWallet.Tests
{
	public class SharedFixture : IDisposable
	{
		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("MagicalCryptoWallet", "Tests"));

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
