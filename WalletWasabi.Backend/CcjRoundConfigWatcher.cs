using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Services;

namespace WalletWasabi.Backend
{
    public class CcjRoundConfigWatcher
    {
		/// <summary>
		/// 0: Not started, 1: Running, 2: Stopping, 3: Stopped
		/// </summary>
		private long _running;
		public bool IsRunning => Interlocked.Read(ref _running) == 1;
		public bool IsStopping => Interlocked.Read(ref _running) == 2;

		private CancellationTokenSource Stop { get; }

		public CcjRoundConfig RoundConfig { get; }
		public CcjCoordinator Coordinator { get; }

		public CcjRoundConfigWatcher(CcjRoundConfig roundConfig, CcjCoordinator coordinator)
		{
			RoundConfig = Guard.NotNull(nameof(roundConfig), roundConfig);
			roundConfig.AssertFilePathSet();
			Coordinator = Guard.NotNull(nameof(coordinator), coordinator);

			_running = 0;
			Stop = new CancellationTokenSource();
		}

		public void Start(TimeSpan period)
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
							if (IsRunning == false) return;

							await Task.Delay(period, Stop.Token);

							if (await RoundConfig.CheckFileChangeAsync())
							{
								Coordinator.FailAllRoundsInInputRegistration();

								await RoundConfig.LoadOrCreateDefaultFileAsync();

								await Coordinator.StartNewRoundAsync(Global.RpcClient, RoundConfig.Denomination, (int)RoundConfig.ConfirmationTarget, (decimal) RoundConfig.CoordinatorFeePercent, (int)RoundConfig.AnonymitySet);
							}
						}
						catch (TaskCanceledException ex)
						{
							Logger.LogTrace<CcjRoundConfigWatcher>(ex);
						}
						catch (Exception ex)
						{
							Logger.LogDebug<CcjRoundConfigWatcher>(ex);
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
