using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers.PowerSaving;

/// <summary><c>systemd-inhibitor</c> API wrapper.</summary>
/// <remarks>Only works on Linux machines that use systemd.</remarks>
/// <seealso href="https://www.freedesktop.org/wiki/Software/systemd/inhibit/"/>
public class LinuxInhibitorTask : IPowerSavingInhibitorTask
{
	/// <remarks>Guarded by <see cref="StateLock"/>.</remarks>
	private bool _isDone;

	/// <remarks>Use the constructor only in tests.</remarks>
	internal LinuxInhibitorTask(InhibitWhat what, TimeSpan period, string reason, ProcessAsync process)
	{
		What = what;
		BasePeriod = period;
		Reason = reason;
		Process = process;
		Cts = new CancellationTokenSource(period);

		_ = WaitAsync();
	}

	/// <summary>Linux GUI environments.</summary>
	public enum GraphicalEnvironment
	{
		Gnome,
		Mate,
		Other,
	}

	[Flags]
	public enum InhibitWhat
	{
		/// <summary>
		/// Inhibits that the system goes into idle mode, possibly resulting in automatic system
		/// suspend or shutdown depending on configuration.
		/// </summary>
		Idle = 1,

		/// <summary>Inhibits system suspend and hibernation requested by (unprivileged) users.</summary>
		Sleep = 2,

		/// <summary>Inhibits high-level system power-off and reboot requested by (unprivileged) users.</summary>
		Shutdown = 4,

		All = Idle | Sleep | Shutdown
	}

	/// <remarks>Guards <see cref="_isDone"/>.</remarks>
	private object StateLock { get; } = new();

	public InhibitWhat What { get; }

	/// <remarks>It holds: inhibitorEndTime = now + BasePeriod + ProlongInterval.</remarks>
	public TimeSpan BasePeriod { get; }

	/// <summary>Reason why the power saving is inhibited.</summary>
	public string Reason { get; }
	private ProcessAsync Process { get; }
	private CancellationTokenSource Cts { get; }
	private TaskCompletionSource StoppedTcs { get; } = new();

	/// <inheritdoc/>
	public bool IsDone
	{
		get
		{
			lock (StateLock)
			{
				return _isDone;
			}
		}
	}

	private async Task WaitAsync()
	{
		try
		{
			await Process.WaitForExitAsync(Cts.Token).ConfigureAwait(false);

			// This should be hit only when somebody externally kills the systemd-inhibit process.
			Logger.LogError("Linux inhibit process ended prematurely.");
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Elapsed time limit for the inhibitor task to live.");
		}
		finally
		{
			if (!Process.HasExited)
			{
				// Process cannot stop on its own so we know it is actually running.
				try
				{
					Process.Kill();
					Logger.LogTrace("Inhibit task was killed.");
				}
				catch (Exception ex)
				{
					Logger.LogTrace($"Failed to kill the process. It might have finished already.", ex);
				}
			}

			lock (StateLock)
			{
				Cts.Cancel();
				Cts.Dispose();
				_isDone = true;
				StoppedTcs.SetResult();
			}

			Logger.LogTrace("Inhibit task is finished.");
		}
	}

	/// <inheritdoc/>
	public bool Prolong(TimeSpan period)
	{
		string logMessage = "N/A";

		try
		{
			lock (StateLock)
			{
				if (!_isDone && !Cts.IsCancellationRequested)
				{
					// This does nothing when cancellation of CTS is already requested.
					Cts.CancelAfter(period);
					logMessage = $"Power saving task was prolonged to: {DateTime.UtcNow.Add(period)}";
					return !Cts.IsCancellationRequested;
				}

				logMessage = "Power saving task is already finished.";
				return false;
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex);
			return false;
		}
		finally
		{
			Logger.LogTrace(logMessage);
		}
	}

	public Task StopAsync()
	{
		lock (StateLock)
		{
			if (!_isDone)
			{
				Cts.Cancel();
			}
		}

		return StoppedTcs.Task;
	}

	public static Task<bool> IsSystemdInhibitSupportedAsync()
	{
		return IsCommandSupportedAsync("systemd-inhibit");
	}

	public static Task<bool> IsGnomeSessionInhibitSupportedAsync()
	{
		return IsCommandSupportedAsync("gnome-session-inhibit");
	}

	public static Task<bool> IsMateSessionInhibitSupportedAsync()
	{
		return IsCommandSupportedAsync("mate-session-inhibit");
	}

	private static async Task<bool> IsCommandSupportedAsync(string command)
	{
		try
		{
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
			ProcessStartInfo processStartInfo = GetProcessStartInfo(command, "--help");
			Process process = System.Diagnostics.Process.Start(processStartInfo)!;

			await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
			bool success = process.ExitCode == 0;
			Logger.LogDebug($"{command} is {(success ? "supported" : "NOT supported")}.");

			return success;
		}
		catch (Exception ex)
		{
			Logger.LogDebug($"Failed to find out whether {command} is supported or not.");
			Logger.LogTrace(ex);
		}

		return false;
	}

	/// <remarks><paramref name="reason"/> cannot contain apostrophe characters.</remarks>
	public static LinuxInhibitorTask Create(InhibitWhat what, TimeSpan basePeriod, string reason, GraphicalEnvironment gui = GraphicalEnvironment.Other)
	{
		// Make sure that the systemd-inhibit is terminated once the parent process (WW) finishes.
		string innerCommand = $"tail --pid={Environment.ProcessId} -f /dev/null";
		string command;
		string arguments;

		// systemd-inhibit command by default does not seem to inhibit idle behavior on Ubuntu 20.04 (and probably most others).
		// That's why we use gnome-session-inhibit when we can.
		if (gui == GraphicalEnvironment.Gnome)
		{
			string inhibitArgument = ConstructInhibitArgument(what);
			command = "gnome-session-inhibit";
			arguments = $"--reason \"{reason}\" --inhibit {inhibitArgument} {innerCommand}";
		}
		else if (gui == GraphicalEnvironment.Mate)
		{
			string inhibitArgument = ConstructInhibitArgument(what);
			command = $"mate-session-inhibit";
			arguments = $"--reason \"{reason}\" --inhibit {inhibitArgument} {innerCommand}";
		}
		else
		{
			string whatArgument = ConstructSystemdWhatArgument(what);
			command = $"systemd-inhibit";
			arguments = $"--why=\"{reason}\" --what=\"{whatArgument}\" --mode=block {innerCommand}";
		}

		Logger.LogTrace($"Command to invoke: {command} {arguments}");
		ProcessStartInfo startInfo = GetProcessStartInfo(command, arguments);

		ProcessAsync process = new(startInfo);
		process.Start();
		LinuxInhibitorTask task = new(what, basePeriod, reason, process);

		return task;
	}

	/// <summary>Constructs argument <c>--inhibit</c> value for <c>gnome-session-inhibit</c> or <c>mate-session-inhibit</c> command.</summary>
	/// <remarks>The possible values are "logout", "switch-user", "suspend", "idle", "automount".</remarks>
	private static string ConstructInhibitArgument(InhibitWhat what)
	{
		List<string> whatList = new();

		if (what.HasFlag(InhibitWhat.Idle))
		{
			whatList.Add("idle");
		}

		if (what.HasFlag(InhibitWhat.Sleep))
		{
			whatList.Add("suspend");
		}

		if (what.HasFlag(InhibitWhat.Shutdown))
		{
			// The best option available probably.
			whatList.Add("logout");
		}

		string whatArgument = string.Join(':', whatList);
		return whatArgument;
	}

	/// <summary>Constructs argument <c>--what</c> value for <c>systemd-inhibit</c> command.</summary>
	private static string ConstructSystemdWhatArgument(InhibitWhat what)
	{
		List<string> whatList = new();

		if (what.HasFlag(InhibitWhat.Idle))
		{
			whatList.Add("idle");
		}

		if (what.HasFlag(InhibitWhat.Sleep))
		{
			whatList.Add("sleep");
		}

		if (what.HasFlag(InhibitWhat.Shutdown))
		{
			whatList.Add("shutdown");
		}

		string whatArgument = string.Join(':', whatList);
		return whatArgument;
	}

	private static ProcessStartInfo GetProcessStartInfo(string command, string arguments)
	{
		return new()
		{
			FileName = command,
			Arguments = arguments,
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden
		};
	}
}
