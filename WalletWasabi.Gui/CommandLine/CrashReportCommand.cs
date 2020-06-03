using Mono.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using Newtonsoft.Json;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.CommandLine
{
	internal class CrashReportCommand : Command
	{
		public CrashReportCommand(Global global) : base("crashreport", "Activates the internal crash reporting mechanism.")
		{
			Global = Guard.NotNull(nameof(Global), global);

			Options = new OptionSet()
			{
				{ "e|exception=", "The serialized exception from the previous crash.", ExceptionDecode }
			};
		}

		private void ExceptionDecode(string exceptionString)
		{
			try
			{
				var e = Guard.NotNullOrEmptyOrWhitespace(nameof(exceptionString), exceptionString);
				ReportedException = JsonConvert.DeserializeObject<Exception>(e);
                
				Global.ShowCrashReporter = true;
				Global.CrashReportException = e;
			}
			catch (Exception ex)
			{
				Logger.LogCritical(ex.Message);
				Environment.Exit(1);
			}
		}

		public Global Global { get; }
		public object ReportedException { get; private set; }

		public override Task<int> InvokeAsync(IEnumerable<string> arguments)
		{
			return base.InvokeAsync(arguments);
		}
	}
}
