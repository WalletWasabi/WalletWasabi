using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases;

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

	/// <summary>
	/// Action successfully executed. Returns how long it took.
	/// </summary>
	public event EventHandler<TimeSpan>? Tick;

	public TimeSpan Period { get; }

	private LastExceptionTracker ExceptionTracker { get; }

	/// <summary>
	/// Normally, <see cref="ActionAsync(CancellationToken)"/> user-action is called every time that <see cref="Period"/> elapses.
	/// This method allows to expedite the process by interrupting the waiting process.
	/// </summary>
	/// <remarks>
	/// If <see cref="ExecuteAsync(CancellationToken)"/> is not actually in waiting phase, this method call makes
	/// sure that next waiting process will be omitted altogether.
	/// <para>Note that when the <see cref="PeriodicRunner"/> has not been started, this call is ignored.</para>
	/// </remarks>
	public void TriggerRound()
	{
		// Note: All members of TaskCompletionSource<TResult> are thread-safe and may be used from multiple threads concurrently.
		_tcs?.TrySetResult(true);
	}

	/// <summary>
	/// Triggers and waits for the action to execute.
	/// </summary>
	public async Task TriggerAndWaitRoundAsync(CancellationToken token)
	{
		EventAwaiter<TimeSpan> eventAwaiter = new(
							h => Tick += h,
							h => Tick -= h);
		TriggerRound();
		await eventAwaiter.WaitAsync(token).ConfigureAwait(false);
	}

	public async Task TriggerAndWaitRoundAsync(TimeSpan timeout)
	{
		using CancellationTokenSource cancellationTokenSource = new(timeout);
		await TriggerAndWaitRoundAsync(cancellationTokenSource.Token).ConfigureAwait(false);
	}

	/// <summary>
	/// Abstract method that is called every <see cref="Period"/> or sooner when <see cref="TriggerRound"/> is called.
	/// </summary>
	/// <remarks>Exceptions are handled in <see cref="ExecuteAsync(CancellationToken)"/>.</remarks>
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
				var before = DateTimeOffset.UtcNow;
				await ActionAsync(stoppingToken).ConfigureAwait(false);
				Tick?.Invoke(this, DateTimeOffset.UtcNow - before);

				ExceptionInfo? info = ExceptionTracker.LastException;

				// Log previous exception if any.
				if (info is { })
				{
					Logger.LogInfo($"Exception stopped coming. It came for " +
						$"{(DateTimeOffset.UtcNow - info.FirstAppeared).TotalSeconds} seconds, " +
						$"{info.ExceptionCount} times: {info.Exception.ToTypeMessageString()}");
					ExceptionTracker.Reset();
				}
			}
			catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
			{
				Logger.LogTrace(ex);
			}
			catch (Exception ex)
			{
				// Exception encountered, process it.
				var info = ExceptionTracker.Process(ex);
				if (info.IsFirst)
				{
					if (info.Exception is HttpRequestException)
					{
						Logger.LogWarning(ex);
					}
					else
					{
						Logger.LogError(info.Exception);
					}
				}
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
					linkedTcs.TrySetCanceled(stoppingToken); // Ensure that the tcs.Task is cleaned up.
				}
			}
			catch (TaskCanceledException ex)
			{
				Logger.LogTrace(ex);
			}
		}
	}
}
