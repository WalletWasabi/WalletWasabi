using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers.PowerSaving;

/// <summary><c>systemd-inhibitor</c> API wrapper.</summary>
/// <remarks>Only works on Linux machines that use systemd.</remarks>
/// <seealso href="https://www.freedesktop.org/wiki/Software/systemd/inhibit/"/>
public class LinuxInhibitorTask : BaseInhibitorTask
{
	private LinuxInhibitorTask(InhibitWhat what, TimeSpan period, string reason, ProcessAsync process)
		: base(period, reason, process)
	{
		What = what;
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

	public InhibitWhat What { get; }

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
}
