using System.Diagnostics;
using WalletWasabi.BundledApps;

namespace WalletWasabi.Helpers.PowerSaving;

/// <summary>
/// Inhibitor based on <c>caffeinate</c> command.
/// </summary>
public class MacOsInhibitorTask : BaseInhibitorTask
{
	private MacOsInhibitorTask(TimeSpan period, string reason, ProcessAsync process)
		: base(period, reason, process)
	{
	}

	public static MacOsInhibitorTask Create(TimeSpan basePeriod, string reason)
	{
		// -w switch makes sure that the caffeinate will stop enforcing "no idle mode" once Wasabi process exits.
		// If Wasabi is killed, orphaned caffeinate 
		var innerCommand = $"""caffeinate -i -w {Environment.ProcessId}""";

		var command = $"/bin/bash";
		var arguments = $"-c \"{innerCommand}\"";

		Logger.LogTrace($"Command to invoke: {command} {arguments}");
		ProcessStartInfo startInfo = GetProcessStartInfo(command, arguments);

		ProcessAsync process = new(startInfo);
		process.Start();
		MacOsInhibitorTask task = new(basePeriod, reason, process);

		return task;
	}
}
