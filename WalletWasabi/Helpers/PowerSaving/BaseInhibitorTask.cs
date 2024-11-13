using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers.PowerSaving;

public class BaseInhibitorTask : IPowerSavingInhibitorTask
{
	/// <remarks>Guarded by <see cref="StateLock"/>.</remarks>
	private bool _isDone;

	protected BaseInhibitorTask(TimeSpan period, string reason, ProcessAsync process)
	{
		BasePeriod = period;
		Reason = reason;
		_process = process;
		_cts = new CancellationTokenSource(period);

		_ = WaitAsync();
	}

	/// <remarks>Guards <see cref="_isDone"/>.</remarks>
	protected object StateLock { get; } = new();

	/// <inheritdoc/>
	public bool IsDone
	{
		get
		{
			lock (StateLock)
			{
				return _isDone;
			}
		}
	}

	/// <remarks>It holds: inhibitorEndTime = now + BasePeriod + ProlongInterval.</remarks>
	public TimeSpan BasePeriod { get; }

	/// <summary>Reason why the power saving is inhibited.</summary>
	public string Reason { get; }

	private readonly ProcessAsync _process;
	private readonly CancellationTokenSource _cts;
	private readonly TaskCompletionSource _stoppedTcs = new();

	private async Task WaitAsync()
	{
		try
		{
			await _process.WaitForExitAsync(_cts.Token).ConfigureAwait(false);

			// This should be hit only when somebody externally kills the inhibiting process.
			Logger.LogError("Inhibit process ended prematurely.");
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Elapsed time limit for the inhibitor task to live.");
		}
		catch (Exception ex)
		{
			Logger.LogDebug(ex);
		}
		finally
		{
			if (!_process.HasExited)
			{
				// _process cannot stop on its own so we know it is actually running.
				try
				{
					_process.Kill(entireProcessTree: true);
					Logger.LogTrace("Inhibit task was killed.");
				}
				catch (Exception ex)
				{
					Logger.LogTrace("Failed to kill the process. It might have finished already.", ex);
				}
			}

			lock (StateLock)
			{
				_cts.Cancel();
				_cts.Dispose();
				_isDone = true;
				_stoppedTcs.SetResult();
			}

			Logger.LogTrace("Inhibit task is finished.");
		}
	}

	/// <inheritdoc/>
	public bool Prolong(TimeSpan period)
	{
		string logMessage = "N/A";

		try
		{
			lock (StateLock)
			{
				if (!_isDone && !_cts.IsCancellationRequested)
				{
					// This does nothing when cancellation of CTS is already requested.
					_cts.CancelAfter(period);
					logMessage = $"Power saving task was prolonged to: {DateTime.UtcNow.Add(period)}";
					return !_cts.IsCancellationRequested;
				}

				logMessage = "Power saving task is already finished.";
				return false;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
		finally
		{
			Logger.LogTrace(logMessage);
		}
	}

	public Task StopAsync()
	{
		lock (StateLock)
		{
			if (!_isDone)
			{
				_cts.Cancel();
			}
		}

		return _stoppedTcs.Task;
	}

	/// <summary>
	/// Checks whether <paramref name="command"/> can be invoked with <c>--help</c> argument
	/// to find out if the command is available on the machine.
	/// </summary>
	protected static async Task<bool> IsCommandSupportedAsync(string command)
	{
		try
		{
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
			ProcessStartInfo processStartInfo = GetProcessStartInfo(command, "--help");
			Process process = System.Diagnostics.Process.Start(processStartInfo)!;

			await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
			bool success = process.ExitCode == 0;
			Logger.LogDebug($"{command} is {(success ? "supported" : "NOT supported")}.");

			return success;
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"Failed to find out whether {command} is supported or not.");
			Logger.LogTrace(ex);
		}

		return false;
	}

	protected static ProcessStartInfo GetProcessStartInfo(string command, string arguments)
	{
		return new()
		{
			FileName = command,
			Arguments = arguments,
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden
		};
	}
}
