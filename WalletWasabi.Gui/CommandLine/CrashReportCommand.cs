using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Gui.CrashReport;

namespace WalletWasabi.Gui.CommandLine
{
	internal class CrashReportCommand : Command
	{
		public CrashReportCommand(CrashReporter crashReporter) : base("crashreport", "Activates the internal crash reporting mechanism.")
		{
			CrashReporter = Guard.NotNull(nameof(crashReporter), crashReporter);

			Options = new OptionSet()
			{
				{ "attempt=", "Number of attempts at starting the crash reporter.", x => Attempts = x },
				{ "exception=", "The serialized exception from the previous crash.", x => ExceptionString = x },
			};
		}

		public CrashReporter CrashReporter { get; }
		public string Attempts { get; private set; }
		public string ExceptionString { get; private set; }

		public override Task<int> InvokeAsync(IEnumerable<string> args)
		{
			try
			{
				Options.Parse(args);

				CrashReporter.SetShowCrashReport(ExceptionString, int.Parse(Attempts));
			}
			catch (Exception ex)
			{
				Task.FromException(ex);
			}

			return Task.FromResult(0);
		}
	}
}
