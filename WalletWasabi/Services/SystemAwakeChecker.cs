using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;
using static WalletWasabi.Helpers.PowerSaving.LinuxInhibitorTask;

namespace WalletWasabi.Services
{
	public class SystemAwakeChecker : PeriodicRunner
	{
		private const string Reason = "CoinJoin is in progress.";
		private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

		private volatile bool _canShutdown = true;

		private volatile IPowerSavingInhibitorTask? _powerSavingTask;

		private SystemAwakeChecker(CoinJoinManager coinJoinManager, Func<Task<IPowerSavingInhibitorTask>>? taskFactory) : base(TimeSpan.FromSeconds(5))
		{
			CoinJoinManager = coinJoinManager;
			TaskFactory = taskFactory;
		}

		public Func<Task<IPowerSavingInhibitorTask>>? TaskFactory { get; }

		public bool CanShutdown
		{
			get => _canShutdown;
			private set => _canShutdown = value;
		}

		private CoinJoinManager CoinJoinManager { get; }

		/// <summary>Checks whether we support awake state prolonging for the current platform.</summary>
		public static async Task<SystemAwakeChecker?> CreateAsync(CoinJoinManager coinJoinManager)
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

				taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(LinuxInhibitorTask.Create(InhibitWhat.Idle, Timeout, Reason, gui));
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(WindowsPowerAvailabilityTask.Create(Reason));
			}

			return new SystemAwakeChecker(coinJoinManager, taskFactory);
		}

		protected async override Task ActionAsync(CancellationToken cancel)
		{
			switch (CoinJoinManager.HighestCoinJoinClientState)
			{
				case CoinJoinClientState.Idle:
					await ReleaseAllPreventionAsync().ConfigureAwait(false);
					break;

				case CoinJoinClientState.InProgress:
					await PreventSleepAsync().ConfigureAwait(false);
					break;

				case CoinJoinClientState.InCriticalPhase:
					PreventShutdown();
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private async Task PreventSleepAsync()
		{
			IPowerSavingInhibitorTask? task = _powerSavingTask;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (task is not null)
				{
					if (!task.Prolong(Timeout.Add(TimeSpan.FromMinutes(1))))
					{
						Logger.LogTrace("Failed to prolong the power saving task.");
						task = null;
					}
				}

				if (task is null)
				{
					Logger.LogTrace("Create new power saving prevention task.");
					_powerSavingTask = await TaskFactory!().ConfigureAwait(false);
				}
			}
			else
			{
				await EnvironmentHelpers.ProlongSystemAwakeAsync().ConfigureAwait(false);
			}
		}

		private async Task ReleaseAllPreventionAsync()
		{
			Logger.LogInfo("Computer idle state is allowed again.");
			CanShutdown = true;
			if (_powerSavingTask is not null)
			{
				await _powerSavingTask.StopAsync().ConfigureAwait(false);
				_powerSavingTask = null;
			}
		}

		// Rest of the work is done on Avalonia's side, in App.xaml.cs and ApplicationViewModel.cs.
		private void PreventShutdown()
		{
			CanShutdown = false;
		}
	}
}
