using System;
using System.Diagnostics;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport
{
	public static class CrashReporter
	{
		public static void Invoke(Exception exceptionToReport)
		{
			try
			{
				var serializedException = exceptionToReport.ToSerializableException();
				var base64ExceptionString = SerializableException.ToBase64String(serializedException);
				var args = $"crashreport -exception=\"{base64ExceptionString}\"";

				var path = Process.GetCurrentProcess().MainModule?.FileName;
				if (string.IsNullOrEmpty(path))
				{
					throw new InvalidOperationException($"Invalid path: '{path}'");
				}

				ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(path, args);
				using Process? p = Process.Start(startInfo);
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"There was a problem while invoking crash report: '{ex}'.");
			}
		}

		public static bool TryGetExceptionFromCliArgs(string[] args, out SerializableException? exception)
		{
			exception = null;
			try
			{
				if (args.Length < 2)
				{
					return false;
				}

				if (args[0].Contains("crashreport") && args[1].Contains("-exception="))
				{
					var exceptionString = args[1].Split("=", StringSplitOptions.RemoveEmptyEntries)[1].Trim('"');

					exception = SerializableException.FromBase64String(exceptionString);
					return true;
				}
			}
			catch (Exception ex)
			{
				// Report the current exception.
				exception = ex.ToSerializableException();

				Logger.LogCritical($"There was a problem: '{ex}'.");
				return true;
			}

			return false;
		}
	}
}
