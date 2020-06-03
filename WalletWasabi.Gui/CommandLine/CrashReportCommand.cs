using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using Newtonsoft.Json;
using WalletWasabi.Logging;
using WalletWasabi.Gui.CrashReporter.Models;

namespace WalletWasabi.Gui.CommandLine
{
	internal class CrashReportCommand : Command
	{
		public CrashReportCommand(Global global) : base("crashreport", "Activates the internal crash reporting mechanism.")
		{
			Global = Guard.NotNull(nameof(Global), global);

			Options = new OptionSet()
			{
				{ "attempt=", "Number of attempts at starting the crash reporter.", x => Attempts = x },
				{ "exception=", "The serialized exception from the previous crash.", x => ExceptionString = x },
			};
		}

		private void ExceptionDecode(string exceptionString)
		{
			var e = Guard.NotNullOrEmptyOrWhitespace(nameof(exceptionString), exceptionString);
			e = e.Trim(' ').Trim('\"');
			Global.CrashReportException = JsonConvert.DeserializeObject<SerializedException>(e);
		}

		public Global Global { get; }
		public string Attempts { get; private set; }
		public string ExceptionString { get; private set; }

		public override async Task<int> InvokeAsync(IEnumerable<string> args)
		{
			Options.Parse(args);

			Global.CrashReportStartAttempt = Attempts;
			
			ExceptionDecode(ExceptionString);
			
			return 0;
		}
	}
}
