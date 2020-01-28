using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.BitcoinCore
{
	public class CoreNodeParams
	{
		public CoreNodeParams(
			Network network,
			MempoolService mempoolService,
			HostedServices hostedServices,
			string dataDir,
			bool tryRestart,
			bool tryDeleteDataDir,
			EndPointStrategy p2pEndPointStrategy,
			EndPointStrategy rpcEndPointStrategy,
			int? txIndex,
			int? prune,
			string userAgent)
		{
			Network = Guard.NotNull(nameof(network), network);
			MempoolService = Guard.NotNull(nameof(mempoolService), mempoolService);
			HostedServices = Guard.NotNull(nameof(hostedServices), hostedServices);
			DataDir = Guard.NotNullOrEmptyOrWhitespace(nameof(dataDir), dataDir);
			TryRestart = tryRestart;
			TryDeleteDataDir = tryDeleteDataDir;
			P2pEndPointStrategy = Guard.NotNull(nameof(p2pEndPointStrategy), p2pEndPointStrategy);
			RpcEndPointStrategy = Guard.NotNull(nameof(rpcEndPointStrategy), rpcEndPointStrategy);
			TxIndex = txIndex;
			Prune = prune;
			UserAgent = Guard.NotNullOrEmptyOrWhitespace(nameof(userAgent), userAgent, trim: true);
		}

		public string DataDir { get; }
		public Network Network { get; }
		public MempoolService MempoolService { get; }
		public HostedServices HostedServices { get; }
		public bool TryRestart { get; }
		public bool TryDeleteDataDir { get; }
		public int? TxIndex { get; }
		public int? Prune { get; }
		public string UserAgent { get; }
		public EndPointStrategy P2pEndPointStrategy { get; }
		public EndPointStrategy RpcEndPointStrategy { get; }
	}
}
