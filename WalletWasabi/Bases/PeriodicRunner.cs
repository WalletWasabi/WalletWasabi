using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	public abstract class PeriodicRunner<T> : NotifyPropertyChangedBase where T : IEquatable<T>
	{
		private T _status;

		public T Status
		{
			get => _status;
			protected set => RaiseAndSetIfChanged(ref _status, value);
		}

		protected CancellationTokenSource Stop { get; set; }
		private CancellationTokenSource Trigger { get; set; }
		private object TriggerLock { get; }
		public TimeSpan Period { get; }
		protected Task ForeverTask { get; set; }
		public Exception LastException { get; set; }
		public long LastExceptionCount { get; set; }
		public DateTimeOffset LastExceptionFirstAppeared { get; set; }

		protected PeriodicRunner(TimeSpan period, T defaultResult)
		{
			Stop = new CancellationTokenSource();
			Trigger = new CancellationTokenSource();
			TriggerLock = new object();
			Period = period;
			ForeverTask = Task.CompletedTask;
			Status = defaultResult;
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
				Trigger?.Cancel();
			}
		}

		protected abstract Task<T> ActionAsync(CancellationToken cancel);

		public void Start()
		{
			ForeverTask = ForeverMethodAsync(x => false);
		}

		protected async Task ForeverMethodAsync(Func<T, bool> finishIf)
		{
			while (!Stop.IsCancellationRequested)
			{
				try
				{
					var status = await ActionAsync(Stop.Token).ConfigureAwait(false);
					Status = status;
					LogAndResetLastExceptionIfNotNull();
					if (finishIf(status))
					{
						Stop?.Cancel();
					}
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
						using var linked = CancellationTokenSource.CreateLinkedTokenSource(Stop.Token, Trigger.Token);
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
							if (Trigger.IsCancellationRequested)
							{
								Trigger?.Dispose();
								Trigger = null;
								Trigger = new CancellationTokenSource();
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
			Stop?.Cancel();
			await ForeverTask.ConfigureAwait(false);
			Stop?.Dispose();
			Stop = null;
			Trigger?.Dispose();
			Trigger = null;
		}
	}
}
