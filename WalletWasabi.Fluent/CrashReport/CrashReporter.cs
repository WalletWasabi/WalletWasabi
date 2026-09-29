using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WalletWasabi.BundledApps;
using WalletWasabi.Client.Configuration;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.CrashReport;

public static class CrashReporter
{
	public static void Invoke(Exception exceptionToReport)
	{
		try
		{
			var serializedException = exceptionToReport.ToSerializableException();
			var base64ExceptionString = SerializableException.ToBase64String(serializedException);
			// For a config that can't be read, the original arguments go along (e.g. --datadir), so the user's choice
			// applies to the same files. The failing instance already started with them, so they are known to parse.
			string[] originalArgs = exceptionToReport is UnreadableConfigException ? Environment.GetCommandLineArgs()[1..] : [];
			string[] args = ["crashreport", $"-exception={base64ExceptionString}", .. originalArgs];

			var path = Process.GetCurrentProcess().MainModule?.FileName;
			if (string.IsNullOrEmpty(path))
			{
				throw new InvalidOperationException($"Invalid path: '{path}'");
			}

			ProcessStartInfo startInfo = ProcessStartInfoFactory.Make(
				processPath: path,
				arguments: args,
				openConsole: false);

			using Process? p = Process.Start(startInfo);
		}
		catch (Exception ex)
		{
			Logger.LogWarning($"There was a problem while invoking crash report: '{ex}'.");
		}
	}

	/// <summary>The arguments the reported instance was started with, see <see cref="Invoke"/>.</summary>
	public static string[] GetOriginalArgs() =>
		Environment.GetCommandLineArgs().Skip(1).Where(x => x != "crashreport" && !x.Contains("-exception=")).ToArray();

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
