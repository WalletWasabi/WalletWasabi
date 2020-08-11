using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	/// <summary>
	/// <see cref="PeriodicRunner"/> is an extension of <see cref="BackgroundService"/> that is useful for tasks
	/// that are supposed to repeat regularly.
	/// </summary>
	public abstract class PeriodicRunner : BackgroundService
	{
		private volatile TaskCompletionSource<bool>? _tcs;

		protected PeriodicRunner(TimeSpan period)
		{
			Period = period;
			ExceptionTracker = new LastExceptionTracker();
		}

		public TimeSpan Period { get; }

		private LastExceptionTracker ExceptionTracker { get; }

		/// <summary>
		/// Normally, <see cref="ActionAsync(CancellationToken)"/> user-action is called every time that <see cref="Period"/> elapses.
		/// This method allows to expedite the process by interrupting the waiting process.
		/// </summary>
		/// <remarks>
		/// If <see cref="ExecuteAsync(CancellationToken)"/> is not actually in waiting phase, this method call makes
		/// sure that next waiting process will be omitted altogether.
		/// </remarks>
		public void TriggerRound()
		{
			// Note: All members of TaskCompletionSource<TResult> are thread-safe and may be used from multiple threads concurrently.
			_tcs?.TrySetResult(true);
		}

		/// <summary>
		/// Abstract method that is called every <see cref="Period"/> or sooner when <see cref="TriggerRound"/> is called.
		/// </summary>
		protected abstract Task ActionAsync(CancellationToken cancel);

		/// <inheritdoc />
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				_tcs = new TaskCompletionSource<bool>();

				try
				{
					// Do user action.
					await ActionAsync(stoppingToken).ConfigureAwait(false);

					// Log previous exception if any.
					LogLastException(ExceptionTracker.LastException);
				}
				catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
				{
					Logger.LogTrace(ex);
				}
				catch (Exception ex)
				{
					// Exception encountered, process it.
					LogLastException(ExceptionTracker.Process(ex));

					Logger.LogError(ExceptionTracker.LastException!.Exception);
				}

				// Wait for the next round.
				try
				{
					using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
					var linkedTcs = _tcs; // Copy reference so it cannot change.

					if (linkedTcs.Task == await Task.WhenAny(linkedTcs.Task, Task.Delay(Period, cts.Token)).ConfigureAwait(false))
					{
						cts.Cancel(); // Ensure that the Task.Delay task is cleaned up.
					}
					else
					{
						linkedTcs.TrySetCanceled(); // Ensure that the tcs.Task is cleaned up.
					}
				}
				catch (TaskCanceledException ex)
				{
					Logger.LogTrace(ex);
				}
			}
		}

		private void LogLastException(ExceptionInfo? info)
		{
			if (info != null)
			{
				Logger.LogInfo($"Exception stopped coming. It came for " +
					$"{(DateTimeOffset.UtcNow - info.FirstAppeared).TotalSeconds} seconds, " +
					$"{info.ExceptionCount} times: {info.Exception.ToTypeMessageString()}");
			}
		}
	}
}
