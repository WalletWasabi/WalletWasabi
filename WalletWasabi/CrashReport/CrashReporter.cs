using System;
using System.Diagnostics;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.CrashReport
{
	public class CrashReporter
	{
		private const int MaxRecursiveCalls = 5;
		public int Attempts { get; private set; }
		public string? Base64ExceptionString { get; private set; } = null;
		public bool HadException { get; private set; }
		public SerializableException? SerializedException { get; private set; }

		public void TryInvokeCrashReport()
		{
			if (!HadException)
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

				var path = Process.GetCurrentProcess()?.MainModule?.FileName;
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

		public bool TryProcessCliArgs(string[] args)
		{
			if (args.Length < 3)
			{
				return false;
			}

			if (args[0].Contains("crashreport") && args[1].Contains("-attempt=") && args[2].Contains("-exception="))
			{
				var attemptString = args[1].Split("=", StringSplitOptions.RemoveEmptyEntries)[1].Trim('"');

				var exceptionString = args[2].Split("=", StringSplitOptions.RemoveEmptyEntries)[1].Trim('"');

				Attempts = int.Parse(attemptString);
				Base64ExceptionString = exceptionString;
				SerializedException = SerializableException.FromBase64String(exceptionString);

				return true;
			}

			return false;
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
