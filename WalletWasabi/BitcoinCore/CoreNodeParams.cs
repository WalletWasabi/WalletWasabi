using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Helpers;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNodeParams
	{
		public CoreNodeParams(
			Network network,
			string dataDir,
			bool tryRestart,
			bool tryDeleteDataDir,
			EndPointStrategy p2pEndPointStrategy,
			EndPointStrategy rpcEndPointStrategy)
		{
			Network = Guard.NotNull(nameof(network), network);
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			TryRestart = tryRestart;
			TryDeleteDataDir = tryDeleteDataDir;
			P2pEndPointStrategy = Guard.NotNull(nameof(p2pEndPointStrategy), p2pEndPointStrategy);
			RpcEndPointStrategy = Guard.NotNull(nameof(rpcEndPointStrategy), rpcEndPointStrategy);
		}

		public string DataDir { get; }
		public Network Network { get; }
		public bool TryRestart { get; }
		public bool TryDeleteDataDir { get; }
		public EndPointStrategy P2pEndPointStrategy { get; }
		public EndPointStrategy RpcEndPointStrategy { get; }
	}
}
