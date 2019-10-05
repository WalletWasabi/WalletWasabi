using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging
{
	public static class Logger
	{
		#region PropertiesAndMembers

		private static long On = 1;

		public static LogLevel MinimumLevel { get; private set; } = LogLevel.Critical;

		public static HashSet<LogMode> Modes { get; } = new HashSet<LogMode>();

		public static string FilePath { get; private set; } = "Log.txt";

		public static string EntrySeparator { get; private set; } = Environment.NewLine;

		/// <summary>
		/// You can use it to identify which software instance created a log entry.
		/// It gets created automatically, but you have to use it manually.
		/// </summary>
		public static Guid InstanceGuid { get; } = Guid.NewGuid();

		private static int LoggingFailedCount = 0;

		private static readonly object Lock = new object();

		/// <summary>
		/// KB
		/// </summary>
		public static long MaximumLogFileSize { get; private set; } = 10_000; // approx 10 MB

		#endregion PropertiesAndMembers

		#region Initializers

		/// <summary>
		/// Initializes the Logger with default values.
		/// If RELEASE then minlevel is info, and logs only to file.
		/// If DEBUG then minlevel is debug, and logs to file, debug and console.
		/// </summary>
		/// <param name="filePath"></param>
		public static void InitializeDefaults(string filePath)
		{
			SetFilePath(filePath);

#if RELEASE
			SetMinimumLevel(LogLevel.Info);
			SetModes(LogMode.Console, LogMode.File);

#else
			SetMinimumLevel(LogLevel.Debug);
			SetModes(LogMode.Debug, LogMode.Console, LogMode.File);
#endif
		}

		public static void SetMinimumLevel(LogLevel level) => MinimumLevel = level;

		public static void SetModes(params LogMode[] modes)
		{
			if (Modes.Count != 0)
			{
				Modes.Clear();
			}

			if (modes is null)
			{
				return;
			}

			foreach (var mode in modes)
			{
				Modes.Add(mode);
			}
		}

		public static void SetFilePath(string filePath) => FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);

		public static void SetEntrySeparator(string entrySeparator) => EntrySeparator = Guard.NotNull(nameof(entrySeparator), entrySeparator);

		/// <summary>
		/// KB
		/// </summary>
		public static void SetMaximumLogFileSize(long sizeInKb) => MaximumLogFileSize = sizeInKb;

		#endregion Initializers

		#region Methods

		public static void TurnOff() => Interlocked.Exchange(ref On, 0);

		public static void TurnOn() => Interlocked.Exchange(ref On, 1);

		public static bool IsOn() => Interlocked.Read(ref On) == 1;

		#endregion Methods

		#region LoggingMethods

		#region GeneralLoggingMethods

		public static void Log(LogLevel level, string message, int additionalEntrySeparators = 0, bool additionalEntrySeparatorsLogFileOnlyMode = true, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			try
			{
				if (Modes.Count == 0 || !IsOn())
				{
					return;
				}

				if (level < MinimumLevel)
				{
					return;
				}

				message = string.IsNullOrWhiteSpace(message) ? "" : message;
				var category = string.IsNullOrWhiteSpace(callerFilePath) ? "" : $"{ExtractFileName(callerFilePath)} ({callerLineNumber})";

				var messageBuilder = new StringBuilder();
				messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss} {level.ToString().ToUpperInvariant()}\t");

				if (message == "")
				{
					if (category == "") // If both empty. It probably never happens though.
					{
						messageBuilder.Append($"{EntrySeparator}");
					}
					else // If only the message is empty.
					{
						messageBuilder.Append($"{category}{EntrySeparator}");
					}
				}
				else
				{
					if (category == "") // If only the category is empty.
					{
						messageBuilder.Append($"{message}{EntrySeparator}");
					}
					else // If none of them empty.
					{
						messageBuilder.Append($"{category}\t{message}{EntrySeparator}");
					}
				}

				var finalMessage = messageBuilder.ToString();

				for (int i = 0; i < additionalEntrySeparators; i++)
				{
					messageBuilder.Insert(0, EntrySeparator);
				}

				var finalFileMessage = messageBuilder.ToString();
				if (!additionalEntrySeparatorsLogFileOnlyMode)
				{
					finalMessage = finalFileMessage;
				}

				lock (Lock)
				{
					if (Modes.Contains(LogMode.Console))
					{
						lock (Console.Out)
						{
							var color = Console.ForegroundColor;
							switch (level)
							{
								case LogLevel.Warning:
									color = ConsoleColor.Yellow;
									break;

								case LogLevel.Error:
								case LogLevel.Critical:
									color = ConsoleColor.Red;
									break;

								default:
									break; // Keep original color.
							}

							Console.ForegroundColor = color;
							Console.Write(finalMessage);
							Console.ResetColor();
						}
					}

					if (Modes.Contains(LogMode.Console))
					{
						Debug.Write(finalMessage);
					}

					if (!Modes.Contains(LogMode.File))
					{
						return;
					}

					IoHelpers.EnsureContainingDirectoryExists(FilePath);

					if (File.Exists(FilePath))
					{
						var sizeInBytes = new FileInfo(FilePath).Length;
						if (sizeInBytes > 1000 * MaximumLogFileSize)
						{
							File.Delete(FilePath);
						}
					}

					File.AppendAllText(FilePath, finalFileMessage);
				}
			}
			catch (Exception ex)
			{
				if (Interlocked.Increment(ref LoggingFailedCount) == 1) // If it only failed the first time, try log the failure.
				{
					LogDebug($"Logging failed: {ex}");
				}
				// If logging the failure is successful then clear the failure counter.
				// If it's not the first time the logging failed, then we do not try to log logging failure, so clear the failure counter.
				Interlocked.Exchange(ref LoggingFailedCount, 0);
			}
		}

		// This method removes the path and file extension.
		//
		// Given Wasabi releases are currently built using Windows, the generated assymblies contain
		// the hardcoded "C:\Users\User\Desktop\WalletWasabi\.......\FileName.cs" string because that
		// is the real path of the file, it doesn't matter what OS was targeted.
		// In Windows and Linux that string is a valid path and that means Path.GetFileNameWithoutExtension
		// can extract the file name but in the case of OSX the same string is not a valid path so, it assumes
		// the whole string is the file name.
		internal static string ExtractFileName(string callerFilePath)
		{
			var lastSeparatorIndex = callerFilePath.LastIndexOf("\\");
			if (lastSeparatorIndex == -1)
			{
				lastSeparatorIndex = callerFilePath.LastIndexOf("/");
			}

			lastSeparatorIndex++;
			var fileNameWithoutExtension = callerFilePath.Substring(lastSeparatorIndex, callerFilePath.Length - lastSeparatorIndex - ".cs".Length);
			return fileNameWithoutExtension;
		}

		#endregion GeneralLoggingMethods

		#region ExceptionLoggingMethods

		private static void Log(Exception ex, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			Log(level, ExceptionToStringHandleNull(ex), callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
		}

		private static string ExceptionToStringHandleNull(Exception ex)
		{
			return ex?.ToString() ?? "Exception was null.";
		}

		#endregion ExceptionLoggingMethods

		#region TraceLoggingMethods

		/// <summary>
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Trace, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion TraceLoggingMethods

		#region DebugLoggingMethods

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Debug level, if only Debug level logging is set.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogDebug(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Debug, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion DebugLoggingMethods

		#region InfoLoggingMethods

		/// <summary>
		/// Logs software starting with InstanceGuid and insert three newlines.
		/// </summary>
		/// <param name="appName">The name of the app.</param>
		public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(LogLevel.Info, $"{appName} started ({InstanceGuid}).", additionalEntrySeparators: 3, additionalEntrySeparatorsLogFileOnlyMode: true, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs software stopped with InstanceGuid.
		/// </summary>
		/// <param name="appName">The name of the app.</param>
		public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(LogLevel.Info, $"{appName} stopped gracefully ({InstanceGuid}).", callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Info, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Info level.
		///
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Info, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion InfoLoggingMethods

		#region WarningLoggingMethods

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Warning level.
		///
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Warning, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion WarningLoggingMethods

		#region ErrorLoggingMethods

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Error level.
		///
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Error, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion ErrorLoggingMethods

		#region CriticalLoggingMethods

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Critical, message, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level.
		///
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) => Log(ex, LogLevel.Critical, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

		#endregion CriticalLoggingMethods

		#endregion LoggingMethods
	}
}
