using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNodeParams
	{
		public CoreNodeParams(Network network, string dataDir, bool tryRestart, bool tryDeleteDataDir)
		{
			Network = Guard.NotNull(nameof(network), network);
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			TryRestart = tryRestart;
			TryDeleteDataDir = tryDeleteDataDir;
		}

		public string DataDir { get; }
		public Network Network { get; }
		public bool TryRestart { get; }
		public bool TryDeleteDataDir { get; }
	}
}
