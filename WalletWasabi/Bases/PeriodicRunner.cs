using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	public abstract class PeriodicRunner
	{
		protected CancellationTokenSource StoppingCts { get; set; }
		private CancellationTokenSource TriggeringCts { get; set; }
		private object TriggerLock { get; }
		public TimeSpan Period { get; }
		protected Task ExecutingTask { get; set; }
		public Exception LastException { get; set; }
		public long LastExceptionCount { get; set; }
		public DateTimeOffset LastExceptionFirstAppeared { get; set; }

		protected PeriodicRunner(TimeSpan period)
		{
			StoppingCts = new CancellationTokenSource();
			TriggeringCts = new CancellationTokenSource();
			TriggerLock = new object();
			Period = period;
			ExecutingTask = Task.CompletedTask;
			ResetLastException();
		}

		private void ResetLastException()
		{
			LastException = null;
			LastExceptionCount = 0;
			LastExceptionFirstAppeared = DateTimeOffset.MinValue;
		}

		public void TriggerRound()
		{
			lock (TriggerLock)
			{
				TriggeringCts?.Cancel();
			}
		}

		protected abstract Task ActionAsync(CancellationToken cancel);

		public void Start()
		{
			ExecutingTask = ForeverMethodAsync();
		}

		protected async Task ForeverMethodAsync()
		{
			while (!StoppingCts.IsCancellationRequested)
			{
				try
				{
					await ActionAsync(StoppingCts.Token).ConfigureAwait(false);
					LogAndResetLastExceptionIfNotNull();
				}
				catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
				{
					Logger.LogTrace(ex);
				}
				catch (Exception ex)
				{
					// Only log one type of exception once.
					if (LastException is null // If the exception never came.
						|| ex.GetType() != LastException.GetType() // Or the exception have different type from previous exception.
						|| ex.Message != LastException.Message) // Or the exception have different message from previous exception.
					{
						// Then log and reset the last exception if another one came before.
						LogAndResetLastExceptionIfNotNull();
						// Set new exception and log it.
						LastException = ex;
						LastExceptionFirstAppeared = DateTimeOffset.UtcNow;
						LastExceptionCount = 1;
						Logger.LogError(ex);
					}
					else
					{
						// Increment the exception counter.
						LastExceptionCount++;
					}
				}
				finally
				{
					try
					{
						using var linked = CancellationTokenSource.CreateLinkedTokenSource(StoppingCts.Token, TriggeringCts.Token);
						await Task.Delay(Period, linked.Token).ConfigureAwait(false);
					}
					catch (TaskCanceledException ex)
					{
						Logger.LogTrace(ex);
					}
					finally
					{
						lock (TriggerLock)
						{
							if (TriggeringCts.IsCancellationRequested)
							{
								TriggeringCts?.Dispose();
								TriggeringCts = null;
								TriggeringCts = new CancellationTokenSource();
							}
						}
					}
				}
			}
		}

		private void LogAndResetLastExceptionIfNotNull()
		{
			if (LastException != null)
			{
				Logger.LogInfo($"Exception stopped coming. It came for {(DateTimeOffset.UtcNow - LastExceptionFirstAppeared).TotalSeconds} seconds, {LastExceptionCount} times: {LastException.ToTypeMessageString()}");
				ResetLastException();
			}
		}

		public async Task StopAsync()
		{
			StoppingCts?.Cancel();
			await ExecutingTask.ConfigureAwait(false);
			StoppingCts?.Dispose();
			StoppingCts = null;
			TriggeringCts?.Dispose();
			TriggeringCts = null;
		}
	}
}
