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
			private set => RaiseAndSetIfChanged(ref _status, value);
		}

		private CancellationTokenSource Stop { get; set; }
		public TimeSpan Period { get; }
		private Task ForeverTask { get; set; }
		public Exception LastException { get; set; }

		protected PeriodicRunner(TimeSpan period, T defaultResult)
		{
			Stop = new CancellationTokenSource();
			Period = period;
			ForeverTask = Task.CompletedTask;
			Status = defaultResult;
			LastException = null;
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
				}
				catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
				{
					Logger.LogTrace(ex);
				}
				catch (Exception ex)
				{
					// Only log one type of exception once.
					if (LastException is null || ex.GetType() != LastException.GetType() || ex.Message != LastException.Message)
					{
						Logger.LogError(ex);
						LastException = ex;
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

		public async Task StopAsync()
		{
			Stop?.Cancel();
			await ForeverTask.ConfigureAwait(false);
			Stop?.Dispose();
			Stop = null;
		}
	}
}
