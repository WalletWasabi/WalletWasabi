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
		try
		{
			// Whenever an exception is thrown, we register in "finally" the crash handler.
			// If this method is not awaited, then the execute task might be null and we might miss crash signals.
			await backgroundService.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			if (terminateService is not null && backgroundService.ExecuteTask is not null)
			{
				_ = backgroundService.ExecuteTask.ContinueWith(
					(Task task) =>
					{
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
