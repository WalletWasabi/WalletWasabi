using System.Reflection;
using Avalonia;
using System;
using WalletWasabi.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.CrashReport.Helpers
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

			var jsonException = JsonConvert.SerializeObject(new SerializableException(e), Formatting.None);

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
