using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui
{
	public static class Global
	{
		private static string _dataDir = null;

		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));

				return _dataDir;
			}
		}

		public static Config Config { get; private set; }

		public async static Task InitializeAsync(Config config)
		{
			Config = Guard.NotNull(nameof(config), config);
		}
	}
}
