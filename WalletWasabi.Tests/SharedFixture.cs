using WalletWasabi.Backend;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Tests.NodeBuilding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WalletWasabi.Tests
{
	public class SharedFixture : IDisposable
	{
		private static string _dataDir = null;
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
			Logger.SetMinimumLevel(LogLevel.Trace);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public void Dispose()
		{
			// Cleanup tests...
		}
	}
}
