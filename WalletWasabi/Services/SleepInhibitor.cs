using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.Helpers;
using WalletWasabi.Helpers.PowerSaving;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using static WalletWasabi.Helpers.PowerSaving.LinuxInhibitorTask;

namespace WalletWasabi.Services;

public class SleepInhibitor : PeriodicRunner
{
	private const string Reason = "Coinjoin is in progress.";
	private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

	private volatile IPowerSavingInhibitorTask? _powerSavingTask;

	private SleepInhibitor(CoinJoinManager coinJoinManager, Func<Task<IPowerSavingInhibitorTask>>? taskFactory) : base(TimeSpan.FromSeconds(5))
	{
		_coinJoinManager = coinJoinManager;
		TaskFactory = taskFactory;
	}

	private readonly CoinJoinManager _coinJoinManager;
	public Func<Task<IPowerSavingInhibitorTask>>? TaskFactory { get; }

	/// <summary>Checks whether we support awake state prolonging for the current platform.</summary>
	public static async Task<SleepInhibitor?> CreateAsync(CoinJoinManager coinJoinManager)
	{
		Func<Task<IPowerSavingInhibitorTask>>? taskFactory = null;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			// Either we have systemd or not.
			bool isSystemd = await IsSystemdInhibitSupportedAsync().ConfigureAwait(false);
			if (!isSystemd)
			{
				return null;
			}

			GraphicalEnvironment gui = GraphicalEnvironment.Other;

			// Specialization for GNOME.
			if (await IsMateSessionInhibitSupportedAsync().ConfigureAwait(false))
			{
				gui = GraphicalEnvironment.Mate;
			}
			else if (await IsGnomeSessionInhibitSupportedAsync().ConfigureAwait(false))
			{
				gui = GraphicalEnvironment.Gnome;
			}

			taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(Create(InhibitWhat.Idle, Timeout, Reason, gui));
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(MacOsInhibitorTask.Create(Timeout, Reason));
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			// Windows 7 does not support the API we use.
			if (Environment.OSVersion.Version.Major < 10)
			{
				return null;
			}

			taskFactory = () => Task.FromResult<IPowerSavingInhibitorTask>(WindowsPowerAvailabilityTask.Create(Reason));
		}

		return new SleepInhibitor(coinJoinManager, taskFactory);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var highestCoinJoinClientState = _coinJoinManager.HighestCoinJoinClientState;
		switch (highestCoinJoinClientState)
		{
			case CoinJoinClientState.Idle:
				Logger.LogTrace("Computer idle state is allowed again.");
				await StopTaskAsync().ConfigureAwait(false);
				break;

			case CoinJoinClientState.InSchedule or CoinJoinClientState.InProgress or CoinJoinClientState.InCriticalPhase:
				await PreventSleepAsync().ConfigureAwait(false);
				break;

			default:
				throw new NotSupportedException($"Unsupported {highestCoinJoinClientState} value.");
		}
	}

	private async Task PreventSleepAsync()
	{
		IPowerSavingInhibitorTask? task = _powerSavingTask;

		if (task is not null)
		{
			if (!task.Prolong(Timeout))
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

	private async Task StopTaskAsync()
	{
		if (_powerSavingTask is not null)
		{
			await _powerSavingTask.StopAsync().ConfigureAwait(false);
			_powerSavingTask = null;
		}
	}
}
