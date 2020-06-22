using System;
using System.Diagnostics;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.CrashReport
{
	public class CrashReporter
	{
		private const int MaxRecursiveCalls = 5;
		public int Attempts { get; private set; }
		public string ExceptionString { get; private set; } = null;
		public bool IsReport { get; private set; }
		public bool HadException { get; private set; }
		public bool IsInvokeRequired => !IsReport && HadException;
		public SerializableException SerializedException { get; private set; }

		public void InvokeCrashReport()
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

		public void SetShowCrashReport(string base64ExceptionString, int attempts)
		{
			Attempts = attempts;
			ExceptionString = base64ExceptionString;
			SerializedException = SerializableException.FromBase64String(ExceptionString);
			IsReport = true;
		}

		/// <summary>
		/// Sets the exception when it occurs the first time and should be reported to the user.
		/// </summary>
		public void SetException(Exception ex)
		{
			SerializedException = ex.ToSerializableException();
			ExceptionString = SerializableException.ToBase64String(SerializedException);
			HadException = true;
		}
	}
}
