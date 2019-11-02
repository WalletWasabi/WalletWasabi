using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNodeParams
	{
		public CoreNodeParams(string dataDir, bool tryRestart, bool tryDeleteDataDir)
		{
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			TryRestart = tryRestart;
			TryDeleteDataDir = tryDeleteDataDir;
		}

		public string DataDir { get; }
		public bool TryRestart { get; }
		public bool TryDeleteDataDir { get; }
	}
}
