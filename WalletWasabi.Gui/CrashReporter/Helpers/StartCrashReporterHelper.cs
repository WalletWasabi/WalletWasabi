using System.Reflection;
using Avalonia;
using System;
using WalletWasabi.Logging;
using Newtonsoft.Json;
using WalletWasabi.Gui.CrashReporter.Models;
using System.Diagnostics;

namespace WalletWasabi.Gui.CrashReporter.Helpers
{
	public static class StartCrashReporterHelper
	{
		public static void Start(Exception e, int prevAttempts)
		{
			if (prevAttempts >= 5)
			{
				Logger.LogCritical("The crash helper has been called 5 times. Will not continue to avoid recursion errors.");
				Environment.Exit(-1);
			}

			var jsonException = JsonConvert.SerializeObject(new SerializedException(e), Formatting.None)
										   .Replace("\'", "\\X0009")
										   .Replace("\"", "\\X0022")
										   .Replace("\n", "\\X000A")
										   .Replace("\r", "\\X000D")
										   .Replace("\t", "\\X0009")
										   .Replace(" ", "\\X0020");

			var args = $"crashreport -attempt={prevAttempts + 1} -exception={jsonException}";

			new Process()
			{
				StartInfo = new ProcessStartInfo()
				{
					FileName = Process.GetCurrentProcess().MainModule.FileName,
					Arguments = $"{args}",
					UseShellExecute = true
				}
			}.Start();

			Environment.Exit(1);
			return;
		}
	}
}