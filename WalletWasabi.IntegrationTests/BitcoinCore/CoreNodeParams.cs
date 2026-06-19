using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.IntegrationTests.BitcoinCore.Endpointing;

namespace WalletWasabi.IntegrationTests.BitcoinCore;

public class CoreNodeParams
{
	public CoreNodeParams(
		Network network,
		MempoolService mempoolService,
		string dataDir,
		bool tryRestart,
		bool tryDeleteDataDir,
		EndPointStrategy p2pEndPointStrategy,
		EndPointStrategy rpcEndPointStrategy,
		int? txIndex,
		int? prune,
		int? disableWallet,
		string? mempoolReplacement,
		string userAgent,
		Money? fallbackFee,
		IMemoryCache cache)
	{
		Network = network;
		MempoolService = mempoolService;
		DataDir = dataDir;
		TryRestart = tryRestart;
		TryDeleteDataDir = tryDeleteDataDir;
		P2pEndPointStrategy = p2pEndPointStrategy;
		RpcEndPointStrategy = rpcEndPointStrategy;
		TxIndex = txIndex;
		Prune = prune;
		DisableWallet = disableWallet;
		MempoolReplacement = mempoolReplacement;
		UserAgent = userAgent;
		FallbackFee = fallbackFee;
		Cache = cache;
	}

	public string DataDir { get; }
	public Network Network { get; }
	public MempoolService MempoolService { get; }
	public bool TryRestart { get; }
	public bool TryDeleteDataDir { get; }
	public int? TxIndex { get; }
	public int? Prune { get; }
	public int? DisableWallet { get; }
	public string? MempoolReplacement { get; }
	public string UserAgent { get; }
	public Money? FallbackFee { get; }
	public int? Listen { get; set; }
	public int? ListenOnion { get; set; }
	public int? Discover { get; set; }
	public int? DnsSeed { get; set; }
	public int? FixedSeeds { get; set; }
	public int? Upnp { get; set; }
	public int? NatPmp { get; set; }
	public int? PersistMempool { get; set; }
	public int? RpcWorkQueue { get; set; }
	public int? RpcThreads { get; set; }
	public int? Port { get; set; }
	public int? BlockFilterIndex { get; set; }
	public int? PeerBlockFilters { get; set; }
	public string? WhiteBindPermissions { get; set; }

	public EndPointStrategy P2pEndPointStrategy { get; }
	public EndPointStrategy RpcEndPointStrategy { get; }
	public IMemoryCache Cache { get; }
}
