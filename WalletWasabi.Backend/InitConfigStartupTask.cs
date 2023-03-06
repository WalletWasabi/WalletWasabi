using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.CoinJoin.Coordinator.Rounds;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend;

public class InitConfigStartupTask : IStartupTask
{
	public InitConfigStartupTask(Global global)
	{
		Global = global;
	}

	private Global Global { get; }

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.InitializeDefaults(Path.Combine(Global.DataDir, "Logs.txt"));
		Logger.LogSoftwareStarted("Wasabi Backend");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		var roundConfigFilePath = Path.Combine(Global.DataDir, "CcjRoundConfig.json");
		var roundConfig = new CoordinatorRoundConfig(roundConfigFilePath);
		roundConfig.LoadFile(createIfMissing: true);
		Logger.LogInfo("RoundConfig is successfully initialized.");

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
