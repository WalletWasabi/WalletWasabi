using Microsoft.Extensions.Caching.Memory;
using NBitcoin.RPC;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Backend;

public class InitConfigStartupTask : IStartupTask
{
	public InitConfigStartupTask(Global global, Config config, IMemoryCache cache)
	{
		Global = global;
		Config = config;
		Cache = cache;
	}

	private Global Global { get; }
	private Config Config { get; }
	private IMemoryCache Cache { get; }

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
		Logger.LogSoftwareStarted("Wasabi Backend");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
		var roundConfig = new CoordinatorRoundConfig(roundConfigFilePath);
		roundConfig.LoadOrCreateDefaultFile();
		Logger.LogInfo("RoundConfig is successfully initialized.");

		string host = Config.GetBitcoinCoreRpcEndPoint().ToString(Config.Network.RPCPort);
		var rpc = new RPCClient(
				authenticationString: Config.BitcoinRpcConnectionString,
				hostOrUri: host,
				network: Config.Network);

		var cachedRpc = new CachedRpcClient(rpc, Cache);
		await Global.InitializeAsync(roundConfig, cancellationToken);
	}

	private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		Logger.LogDebug(e.Exception);
	}

	private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception ex)
		{
			Logger.LogWarning(ex);
		}
	}
}
