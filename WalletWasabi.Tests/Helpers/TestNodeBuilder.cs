using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.Helpers;

public static class TestNodeBuilder
{
	public static async Task<CoreNode> CreateAsync(string path)
	{
		var dataDir = Common.GetWorkDir(path);

		CoreNodeParams nodeParameters = CreateDefaultCoreNodeParams(new MempoolService(), dataDir);
		return await CoreNode.CreateAsync(
			nodeParameters,
			CancellationToken.None);
	}

	public static async Task<CoreNode> CreateForHeavyConcurrencyAsync(string path)
	{
		var dataDir = Common.GetWorkDir(path);

		CoreNodeParams nodeParameters = CreateDefaultCoreNodeParams(new MempoolService(), dataDir);
		nodeParameters.RpcWorkQueue = 32;
		return await CoreNode.CreateAsync(
			nodeParameters,
			CancellationToken.None);
	}

	private static CoreNodeParams CreateDefaultCoreNodeParams(MempoolService mempoolService, string dataDir)
	{
		var nodeParameters = new CoreNodeParams(
				Network.RegTest,
				mempoolService,
				dataDir,
				tryRestart: true,
				tryDeleteDataDir: true,
				EndPointStrategy.Random,
				EndPointStrategy.Random,
				txIndex: 1,
				prune: 0,
				disableWallet: 0,
				mempoolReplacement: "fee,optin",
				userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
				fallbackFee: Money.Coins(0.0002m), // https://github.com/bitcoin/bitcoin/pull/16524
				new MemoryCache(new MemoryCacheOptions()));
		nodeParameters.ListenOnion = 0;
		nodeParameters.Discover = 0;
		nodeParameters.DnsSeed = 0;
		nodeParameters.FixedSeeds = 0;
		nodeParameters.Upnp = 0;
		nodeParameters.NatPmp = 0;
		nodeParameters.PersistMempool = 0;
		return nodeParameters;
	}
}
