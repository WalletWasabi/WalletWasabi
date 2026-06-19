using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.IntegrationTests.BitcoinCore;
using WalletWasabi.IntegrationTests.BitcoinCore.Endpointing;

namespace WalletWasabi.IntegrationTests.Infrastructure;

public static class TestNodeBuilder
{
	public static readonly EventBus EventBus = new();

	public static async Task<CoreNode> CreateAsync([CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", string additionalFolder = "")
	{
		var dataDir = Path.Combine(GetWorkDir(callerFilePath, callerMemberName), additionalFolder);
		var mempoolService = new MempoolService(EventBus);
		var nodeParameters = CreateDefaultCoreNodeParams(mempoolService, dataDir);

		return await CoreNode.CreateAsync(nodeParameters, CancellationToken.None).ConfigureAwait(false);
	}

	private static CoreNodeParams CreateDefaultCoreNodeParams(MempoolService mempoolService, string dataDir)
	{
#pragma warning disable CA2000 // Dispose objects before losing scope - MemoryCache ownership transferred to CoreNodeParams
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
#pragma warning restore CA2000
		nodeParameters.ListenOnion = 0;
		nodeParameters.Discover = 0;
		nodeParameters.DnsSeed = 0;
		nodeParameters.FixedSeeds = 0;
		nodeParameters.Upnp = 0;
		nodeParameters.NatPmp = 0;
		nodeParameters.PersistMempool = 0;
		nodeParameters.Port = PortFinder.GetRandomPorts(1)[0];
		nodeParameters.BlockFilterIndex = 1; // Enable compact block filters for integration tests
		nodeParameters.PeerBlockFilters = 1; // Enable serving compact filters via P2P (BIP 157)
		nodeParameters.WhiteBindPermissions = "noban,download"; // Allow P2P connections without banning
		return nodeParameters;
	}

	private static string GetWorkDir(string callerFilePath, string callerMemberName)
	{
		var dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "IntegrationTests"));
		return Path.Combine(dataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName);
	}
}
