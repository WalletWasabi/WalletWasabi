using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WalletWasabi.Gui.CrashReport.Models;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Gui.CrashReport
{
	public class CrashReporter
	{
		private const int MaxRecursiveCalls = 5;

		public int Attempts { get; private set; }
		public string ExceptionString { get; private set; } = null;
		public bool IsReport => ExceptionString is { };

		public void Start()
		{
			var exceptionString = ExceptionString;
			var prevAttempts = Attempts;

			if (prevAttempts >= MaxRecursiveCalls)
			{
				Logger.LogCritical($"The crash helper has been called {MaxRecursiveCalls} times. Will not continue to avoid recursion errors.");
				return;
			}

			var args = $"crashreport -attempt={prevAttempts + 1} -exception={exceptionString}";

			ProcessBridge processBridge = new ProcessBridge(Process.GetCurrentProcess().MainModule.FileName);
			processBridge.Start(args, false);

			return;
		}

		public void SetException(string exceptionString, int attempts)
		{
			Attempts = attempts;
			ExceptionString = exceptionString;
		}

		/// <summary>
		/// Sets the exception when it occurs the first time and should be reported to the user.
		/// </summary>
		public void SetException(Exception ex)
		{
			SetException(SerializedException.ToCommandLineString(ex), 1);
		}

		public override string ToString()
		{
			var exceptionToDisplay = Guard.NotNullOrEmptyOrWhitespace(nameof(ExceptionString), ExceptionString);
			exceptionToDisplay = exceptionToDisplay.Replace("\\X0009", "\'")
					 .Replace("\\X0022", "\"")
					 .Replace("\\X000A", "\n")
					 .Replace("\\X000D", "\r")
					 .Replace("\\X0009", "\t")
					 .Replace("\\X0020", " ");

			return exceptionToDisplay;
		}

		public SerializedException GetException()
		{
			return SerializedException.ToSerializedException(ExceptionString);
		}
	}
}
