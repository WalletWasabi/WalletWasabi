using System;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;

namespace WalletWasabi.Services
{
	public class ConfigWatcher
	{
		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;

		public bool IsRunning => Interlocked.Read(ref _running) == 1;

		private CancellationTokenSource Stop { get; set; }

		public IConfig Config { get; }

		public ConfigWatcher(IConfig config)
		{
			Config = Guard.NotNull(nameof(config), config);
			config.AssertFilePathSet();

			_running = 0;
			Stop = new CancellationTokenSource();
		}

		public void Start(TimeSpan period, Func<Task> executeWhenChangedAsync)
		{
			if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
			{
				Task.Run(async () =>
				{
					try
					{
						while (IsRunning)
						{
							try
							{
								await Task.Delay(period, Stop.Token);

								if (await Config.CheckFileChangeAsync())
								{
									await Config.LoadOrCreateDefaultFileAsync();

									await executeWhenChangedAsync?.Invoke();
								}
							}
							catch (TaskCanceledException ex)
							{
								Logger.LogTrace<ConfigWatcher>(ex);
							}
							catch (Exception ex)
							{
								Logger.LogDebug<ConfigWatcher>(ex);
							}
						}
					}
					finally
					{
						Interlocked.CompareExchange(ref _running, 3, 2); // If IsStopping, make it stopped.
					}
				});
			}
		}

		public async Task StopAsync()
		{
			Interlocked.CompareExchange(ref _running, 2, 1); // If running, make it stopping.
			Stop?.Cancel();
			while (Interlocked.CompareExchange(ref _running, 3, 0) == 2)
			{
				await Task.Delay(50);
			}

			Stop?.Dispose();
			Stop = null;
		}
	}
}
