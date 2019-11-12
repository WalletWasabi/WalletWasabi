using System;
using System.Collections.Generic;
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

		private CancellationTokenSource Stop { get; set; }
		public TimeSpan Period { get; }
		private Task ForeverTask { get; set; }
		public Exception LastException { get; set; }
		public long LastExceptionCount { get; set; }
		public DateTimeOffset LastExceptionFirstAppeared { get; set; }

		protected PeriodicRunner(TimeSpan period, T defaultResult)
		{
			Stop = new CancellationTokenSource();
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

		public abstract Task<T> ActionAsync(CancellationToken cancel);

		public void Start()
		{
			ForeverTask = ForeverMethodAsync();
		}

		private async Task ForeverMethodAsync()
		{
			while (!Stop.IsCancellationRequested)
			{
				try
				{
					Status = await ActionAsync(Stop.Token).ConfigureAwait(false);
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
						await Task.Delay(Period, Stop.Token).ConfigureAwait(false);
					}
					catch (TaskCanceledException ex)
					{
						Logger.LogTrace(ex);
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
		}
	}
}
