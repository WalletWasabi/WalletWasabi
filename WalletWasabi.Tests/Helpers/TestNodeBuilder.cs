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
using WalletWasabi.Services;

namespace WalletWasabi.Tests.Helpers
{
	public static class TestNodeBuilder
	{
		public static async Task<CoreNode> CreateAsync(HostedServices hostedServices, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", string additionalFolder = "", MempoolService? mempoolService = null)
		{
			var network = Network.RegTest;
			return await CoreNode.CreateAsync(
				new CoreNodeParams(
					network,
					mempoolService ?? new MempoolService(),
					hostedServices,
					Path.Combine(Common.GetWorkDir(callerFilePath, callerMemberName), additionalFolder),
					tryRestart: true,
					tryDeleteDataDir: true,
					EndPointStrategy.Random,
					EndPointStrategy.Random,
					txIndex: 1,
					prune: 0,
					daemon: 0,
					mempoolReplacement: "fee,optin",
					userAgent: $"/WasabiClient:{Constants.ClientVersion}/",
					fallbackFee: Money.Coins(0.0002m), // https://github.com/bitcoin/bitcoin/pull/16524
					new MemoryCache(new MemoryCacheOptions())),
				CancellationToken.None);
		}
	}
}
