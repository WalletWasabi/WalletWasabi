using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend;

public class InitConfigStartupTask : IStartupTask
{
	public InitConfigStartupTask(Global global)
	{
		_global = global;
	}

	private readonly Global _global;

	public async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		Logger.InitializeDefaults(Path.Combine(_global.DataDir, "Logs.txt"));
		Logger.LogSoftwareStarted("Wasabi Backend");

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

		await _global.InitializeAsync(cancellationToken);
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
