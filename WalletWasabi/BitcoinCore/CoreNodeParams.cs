using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.Services;

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
			string mempoolReplacement,
			string userAgent,
			Money fallbackFee,
			IMemoryCache cache)
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
			MempoolReplacement = mempoolReplacement;
			UserAgent = Guard.NotNullOrEmptyOrWhitespace(nameof(userAgent), userAgent, trim: true);
			FallbackFee = fallbackFee;
			Cache = Guard.NotNull(nameof(cache), cache);
		}

		public string DataDir { get; }
		public Network Network { get; }
		public MempoolService MempoolService { get; }
		public HostedServices HostedServices { get; }
		public bool TryRestart { get; }
		public bool TryDeleteDataDir { get; }
		public int? TxIndex { get; }
		public int? Prune { get; }
		public string MempoolReplacement { get; }
		public string UserAgent { get; }
		public Money FallbackFee { get; }
		public int? Listen { get; set; }
		public int? ListenOnion { get; set; }
		public int? Discover { get; set; }
		public int? DnsSeed { get; set; }
		public int? FixedSeeds { get; set; }
		public int? Upnp { get; set; }
		public int? NatPmp { get; set; }
		public int? PersistMempool { get; set; }
		public EndPointStrategy P2pEndPointStrategy { get; }
		public EndPointStrategy RpcEndPointStrategy { get; }
		public IMemoryCache Cache { get; }
	}
}
