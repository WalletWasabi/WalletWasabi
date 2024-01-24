using System.Diagnostics;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

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
		// The script is written with newlines but we need to pass the script to
		// bash -c as a one-liner, so that's why we use the "ReplaceLineEndings" command.
		//
		// Note that this script requires dollar signs ($) and quotes (") to be escaped.
		string innerCommand = $$"""
			caffeinate -i &
			caffeinatePid=$!;
			trap \"kill -9 \$caffeinatePid\" 0 SIGINT SIGTERM;
			while (ps -p {{Environment.ProcessId}} );
			do
				sleep 1;
			done;
			""".ReplaceLineEndings(replacementText: " ");

		string command = $"/bin/bash";
		string arguments = $"-c \"{innerCommand}\"";

		Logger.LogTrace($"Command to invoke: {command} {arguments}");
		ProcessStartInfo startInfo = GetProcessStartInfo(command, arguments);

		ProcessAsync process = new(startInfo);
		process.Start();
		MacOsInhibitorTask task = new(basePeriod, reason, process);

		return task;
	}
}
