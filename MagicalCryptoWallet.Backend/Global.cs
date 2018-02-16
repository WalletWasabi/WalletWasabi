using MagicalCryptoWallet.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Backend
{
	public static class Global
	{
		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir("MagicalCryptoWalletBackend");

				return _dataDir;
			}
		}

		public static Config Config { get; private set; }

		public async static Task InitializeAsync()
		{
			_dataDir = null;

			await InitializeConfigAsync();
		}

		public static async Task InitializeConfigAsync()
		{
			string configFilePath = Path.Combine(DataDir, "Config.json");
			Config = new Config();
			await Config.LoadOrCreateDefaultFileAsync(configFilePath);
		}
	}
}
