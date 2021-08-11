using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
				StartWasabiWithArgs(args);
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"There was a problem while invoking crash report: '{ex}'.");
			}
		}

		public static void RestartWasabi()
		{
			Task.Run(async () =>
			{
				await Task.Delay(1000);
				ShutdownWasabi();
			});

			StartWasabiWithArgs();
		}

		public static void StartWasabiWithArgs(string args = "")
		{
			var path = Process.GetCurrentProcess().MainModule?.FileName;
			if (string.IsNullOrEmpty(path))
			{
				throw new InvalidOperationException($"Invalid path: '{path}'");
			}
			var startInfo = ProcessStartInfoFactory.Make(path, args);
			using var p = Process.Start(startInfo);
		}

		public static bool TryGetExceptionFromCliArgs(string[] args, [NotNullWhen(true)] out SerializableException? exception)
		{
			exception = null;
			try
			{
				if (args.Length < 2)
				{
					return false;
				}

				var arg1 = args.SingleOrDefault(x => x == "crashreport");
				var arg2 = args.SingleOrDefault(x => x.Contains("-exception="));

				if (arg1 is not null && arg2 is not null)
				{
					var exceptionString = arg2.Split("=", count: 2)[1].Trim('"');

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

		public static void ShutdownWasabi()
		{
			(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}
	}
}