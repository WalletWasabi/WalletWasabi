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
			var exS = Guard.NotNullOrEmptyOrWhitespace(nameof(exceptionString), exceptionString);
			
			exS = exS.Replace("\\X0009", "\'")
					 .Replace("\\X0022", "\"")
					 .Replace("\\X000A", "\n")
					 .Replace("\\X000D", "\r")
					 .Replace("\\X0009", "\t")
					 .Replace("\\X0020", " ");
			 
			Global.CrashReportException = JsonConvert.DeserializeObject<SerializedException>(exS);
		}

		public Global Global { get; }
		public string Attempts { get; private set; }
		public string ExceptionString { get; private set; }

		public override async Task<int> InvokeAsync(IEnumerable<string> args)
		{
			Options.Parse(args);

			Global.CrashReportStartAttempt = Convert.ToInt32(Attempts);

			ExceptionDecode(ExceptionString);

			return 0;
		}
	}
}
