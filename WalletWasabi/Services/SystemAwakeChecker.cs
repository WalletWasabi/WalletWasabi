using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using static WalletWasabi.Helpers.PowerSaving.LinuxInhibitorTask;

namespace WalletWasabi.Services
{
	public class SystemAwakeChecker : PeriodicRunner
	{
		private const string Reason = "CoinJoin is in progress.";
		private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

		private volatile IPowerSavingInhibitorTask? _powerSavingTask;

		private SystemAwakeChecker(WalletManager walletManager, Func<Task<IPowerSavingInhibitorTask>>? taskFactory) : base(TimeSpan.FromMinutes(1))
		{
			WalletManager = walletManager;
			TaskFactory = taskFactory;
		}

		private WalletManager WalletManager { get; }
		public Func<Task<IPowerSavingInhibitorTask>>? TaskFactory { get; }

		/// <summary>Checks whether we support awake state prolonging for the current platform.</summary>
		public static async Task<SystemAwakeChecker?> CreateAsync(WalletManager walletManager)
		{
			Func<Task<IPowerSavingInhibitorTask>>? taskFactory = null;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// Either we have systemd or not.
				bool isSystemd = await LinuxInhibitorTask.IsSystemdInhibitSupportedAsync().ConfigureAwait(false);
				if (!isSystemd)
				{
					return null;
				}

				GraphicalEnvironment gui = GraphicalEnvironment.Other;

				// Specialization for GNOME.
				if (await LinuxInhibitorTask.IsMateSessionInhibitSupportedAsync().ConfigureAwait(false))
				{
					gui = GraphicalEnvironment.Mate;
				}
				else if (await LinuxInhibitorTask.IsGnomeSessionInhibitSupportedAsync().ConfigureAwait(false))
				{
					gui = GraphicalEnvironment.Gnome;
				}

				taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(LinuxInhibitorTask.Create(InhibitWhat.All, Timeout, Reason, gui));
			}

			return new SystemAwakeChecker(walletManager, taskFactory);
		}

		protected override async Task ActionAsync(CancellationToken cancel)
		{
			IPowerSavingInhibitorTask? task = _powerSavingTask;

			if (WalletManager.AnyCoinJoinInProgress())
			{
				Logger.LogWarning($"XXX: CJ is in progress, make sure to prolong awake state.");

				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					if (task is not null)
					{
						Logger.LogWarning($"XXX: Prolong power saving prevention task by one minute.");
						if (!task.Prolong(TimeSpan.FromMinutes(1)))
						{
							Logger.LogWarning($"XXX: Failed to prolong.");
							task = null;
						}
					}

					if (task is null)
					{
						Logger.LogWarning($"XXX: Create new power saving prevention task.");
						_powerSavingTask = await TaskFactory!().ConfigureAwait(false);
					}
				}
				else
				{
					await EnvironmentHelpers.ProlongSystemAwakeAsync().ConfigureAwait(false);
				}
			}
			else
			{
				if (task is not null)
				{
					Logger.LogWarning($"XXX: Stop task.");
					await task.StopAsync().ConfigureAwait(false);
					_powerSavingTask = null;
				}
			}
		}
	}
}
