using MagicalCryptoWallet.Backend;
using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Tests.NodeBuilding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
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

		public string BackendEndPoint { get; internal set; }

		public IWebHost BackendHost { get; internal set; }

		public NodeBuilder BackendNodeBuilder { get; internal set; }

		public CoreNode BackendRegTestNode { get; internal set; }

		public SharedFixture()
		{
			// Initialize tests...

			BackendEndPoint = null;
			BackendHost = null;
			BackendNodeBuilder = null;
			BackendRegTestNode = null;

			Logger.SetFilePath(Path.Combine(DataDir, "Logs.txt"));
			Logger.SetMinimumLevel(LogLevel.Info);
			Logger.SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
		}

		public void Dispose()
		{
			// Cleanup tests...

			BackendHost?.StopAsync();
			BackendHost?.Dispose();
			BackendRegTestNode?.Kill(cleanFolder: true);
			BackendNodeBuilder?.Dispose();
		}
	}
}
