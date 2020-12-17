using System;
using System.Diagnostics;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.CrashReport
{
	public class CrashReporter
	{
		private const int MaxRecursiveCalls = 3;
		public int Attempts { get; private set; }
		private string? Base64ExceptionString { get; set; } = null;

		/// <summary>
		/// The current Wasabi instance had an exception.
		/// </summary>
		public bool HadException { get; private set; }

		public SerializableException? SerializedException { get; private set; }

		/// <summary>
		/// Call this right before the end of the application. It will call another Wasabi instance in crash report mode if there is something to report.
		/// The exception can be set by the CLI arguments by calling <see cref="ProcessCliArgs(string[])"/> or <see cref="SetException(Exception)"/>.
		/// </summary>
		public void TryInvokeIfRequired()
		{
			if (SerializedException is null)
			{
				return;
			}

			try
			{
				if (Attempts >= MaxRecursiveCalls)
				{
					throw new InvalidOperationException($"The crash report has been called {MaxRecursiveCalls} times. Will not continue to avoid recursion errors.");
				}

				var args = ToCliArguments();

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
				Logger.LogWarning($"There was a problem while invoking crash report:{ex.ToUserFriendlyString()}.");
			}
		}

		public string ToCliArguments()
		{
			if (string.IsNullOrEmpty(Base64ExceptionString))
			{
				throw new InvalidOperationException($"The crash report exception message is empty.");
			}

			return $"crashreport -attempt=\"{Attempts + 1}\" -exception=\"{Base64ExceptionString}\"";
		}

		public SerializableException? ProcessCliArgs(string[] args)
		{
			if (args.Length < 3)
			{
				return null;
			}

			if (args[0].Contains("crashreport") && args[1].Contains("-attempt=") && args[2].Contains("-exception="))
			{
				var attemptString = args[1].Split("=", StringSplitOptions.RemoveEmptyEntries)[1].Trim('"');

				var exceptionString = args[2].Split("=", StringSplitOptions.RemoveEmptyEntries)[1].Trim('"');

				Attempts = int.Parse(attemptString);

				return SerializableException.FromBase64String(exceptionString);
			}

			return null;
		}

		/// <summary>
		/// Sets the exception when it occurs the first time and should be reported to the user.
		/// </summary>
		public void SetException(Exception ex)
		{
			SerializedException = ex.ToSerializableException();
			Base64ExceptionString = SerializableException.ToBase64String(SerializedException);
			HadException = true;
		}
	}
}
