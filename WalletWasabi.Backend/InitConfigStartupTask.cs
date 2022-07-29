using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using NBitcoin;
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
	public InitConfigStartupTask(Global global, IMemoryCache cache)
	{
		Global = global;
		Cache = cache;
	}

	public Global Global { get; }
	public IMemoryCache Cache { get; }

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
		Logger.LogSoftwareStarted("Wasabi Backend");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		var configFilePath = Path.Combine(Global.DataDir, "Config.json");
		var config = new Config(configFilePath);
		config.LoadOrCreateDefaultFile();
		Logger.LogInfo("Config is successfully initialized.");

		var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
		var roundConfig = new CoordinatorRoundConfig(roundConfigFilePath);
		roundConfig.LoadOrCreateDefaultFile();
		Logger.LogInfo("RoundConfig is successfully initialized.");

		string host = config.GetBitcoinCoreRpcEndPoint().ToString(config.Network.RPCPort);
		var rpc = new RPCClient(
				authenticationString: config.BitcoinRpcConnectionString,
				hostOrUri: host,
				network: config.Network);

		var cachedRpc = new CachedRpcClient(rpc, Cache);
		await Global.InitializeAsync(config, roundConfig, cachedRpc, cancellationToken);
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
