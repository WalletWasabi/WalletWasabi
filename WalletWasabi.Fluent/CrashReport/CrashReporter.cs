using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport;

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

			ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(path, args, true);
			using Process? p = Process.Start(startInfo);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"There was a problem while invoking crash report: '{ex}'.");
		}
	}

	public static bool TryGetExceptionFromCliArgs(string[] args, [NotNullWhen(true)] out SerializableException? exception)
	{
		exception = null;
		try
		{
			var commandArgument = args.SingleOrDefault(x => x == "crashreport");
			var parameterArgument = args.SingleOrDefault(x => x.Contains("-exception="));

			if (commandArgument is not null && parameterArgument is not null)
			{
				var exceptionString = parameterArgument.Split("=", count: 2)[1].Trim('"');

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
