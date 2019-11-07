using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;

namespace WalletWasabi.Bases
{
	public abstract class PeriodicRunner : NotifyPropertyChangedBase
	{
		private object _status;

		public object Status
		{
			get => _status;
			private set => RaiseAndSetIfChanged(ref _status, value);
		}

		private CancellationTokenSource Stop { get; set; }
		public TimeSpan Period { get; }
		private Task ForeverTask { get; set; }

		protected PeriodicRunner(TimeSpan period, object defaultResult)
		{
			Stop = new CancellationTokenSource();
			Period = period;
			ForeverTask = Task.CompletedTask;
			Status = defaultResult;
		}

		public abstract Task<object> ActionAsync(CancellationToken cancel);

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
					Logger.LogError(ex);
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
