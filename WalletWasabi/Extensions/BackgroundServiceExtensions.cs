using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Services.Terminate;

namespace WalletWasabi.Extensions;

/// <summary>
/// Extensions for <see cref="BackgroundService"/>.
/// </summary>
public static class BackgroundServiceExtensions
{
	/// <summary>
	/// Triggered when the application host is ready to start the service AND registers a callback to shut down gracefully on a service crash.
	/// </summary>
	/// <param name="backgroundService">Background service to start.</param>
	/// <param name="terminateService">Termination service to use to signal graceful shutdown, or <c>null</c> not to register any callback at all.</param>
	/// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
	/// <returns>A <see cref="Task"/> that represents the asynchronous Start operation.</returns>
	public static async Task StartAndSetUpUnhandledExceptionCallbackAsync(this BackgroundService backgroundService, ITerminateService? terminateService, CancellationToken cancellationToken)
	{
		Task startTask = backgroundService.StartAsync(cancellationToken);

		try
		{
			// Whenever an exception is thrown, we register in "finally" the crash handler.
			// If this method is not awaited, then the execute task might be null and we might miss crash signals.
			await startTask.ConfigureAwait(false);
		}
		finally
		{
			// If StartAsync crashes with an exception, then continue with that task, otherwise the start task ran to completion and we should monitor the execute task.
			Task? taskToContinue = startTask.IsFaulted ? startTask : backgroundService.ExecuteTask;

			if (terminateService is not null && taskToContinue is not null)
			{
				_ = taskToContinue.ContinueWith(
					(Task task) =>
					{
						Logger.LogWarning($"'{backgroundService.GetType()?.FullName}' continue.");

						if (task.IsFaulted)
						{
							Logger.LogWarning($"Signal graceful termination because '{backgroundService.GetType()?.FullName}' crashed.");
							Exception ex = task.Exception.InnerException is not null
								? task.Exception.InnerException
								: task.Exception;

							terminateService.SignalServiceCrash(ex);
						}

						return Task.CompletedTask;
					},
					cancellationToken);
			}
		}
	}
}
