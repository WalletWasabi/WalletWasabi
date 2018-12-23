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
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Stop { get; }

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
			Interlocked.Exchange(ref _running, 1);

			Task.Run(async () =>
			{
				try
				{
					while (IsRunning)
					{
						try
						{
							// If stop was requested return.
							if (!IsRunning) return;

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
					if (IsStopping)
					{
						Interlocked.Exchange(ref _running, 3);
					}
				}
			});
		}

		public async Task StopAsync()
		{
			if (IsRunning)
			{
				Interlocked.Exchange(ref _running, 2);
			}
			Stop?.Cancel();
			while (IsStopping)
			{
				await Task.Delay(50);
			}
			Stop?.Dispose();
		}
	}
}
