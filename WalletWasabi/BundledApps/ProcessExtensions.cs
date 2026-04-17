using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.BundledApps;

public static class ProcessExtensions 
{
	public static void StartAndLogException(this Process process)
	{
		try
		{
			process.Start();
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);

			Logger.LogInfo($"{nameof(Process.StartInfo.FileName)}: {process.StartInfo.FileName}.");
			Logger.LogInfo($"{nameof(Process.StartInfo.Arguments)}: {process.StartInfo.Arguments}.");
			Logger.LogInfo($"{nameof(Process.StartInfo.RedirectStandardOutput)}: {process.StartInfo.RedirectStandardOutput}.");
			Logger.LogInfo($"{nameof(Process.StartInfo.UseShellExecute)}: {process.StartInfo.UseShellExecute}.");
			Logger.LogInfo($"{nameof(Process.StartInfo.CreateNoWindow)}: {process.StartInfo.CreateNoWindow}.");
			Logger.LogInfo($"{nameof(Process.StartInfo.WindowStyle)}: {process.StartInfo.WindowStyle}.");
			throw;
		}
	}

	/// <summary>
	/// Waits until the process either finishes on its own or when user cancels the action.
	/// </summary>
	public static async Task GracefulWaitForExitAsync(this Process process, CancellationToken cancellationToken)
	{
		if (process.HasExited)
		{
			Logger.LogTrace("Process has already exited.");
			return;
		}

		try
		{
			Logger.LogTrace($"Wait for the process to exit: '{process.Id}'");
			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

			Logger.LogTrace("Process has exited.");
		}
		catch (OperationCanceledException ex)
		{
			Logger.LogTrace("User canceled waiting for process exit.");
			throw new TaskCanceledException("Waiting for process exiting was canceled.", ex, cancellationToken);
		}
	}
}
