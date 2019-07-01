using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

		private static void Log(LogLevel level, string message, string category, int additionalEntrySeparators = 0, bool additionalEntrySeparatorsLogFileOnlyMode = true)
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
				category = string.IsNullOrWhiteSpace(category) ? "" : category;

				var messageBuilder = new StringBuilder();
				messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss} {level.ToString().ToUpperInvariant()}");

				if (message != "" && category != "") // If none of them empty.
				{
					messageBuilder.Append($" {category}: {message}{EntrySeparator}");
				}
				else if (message == "" && category != "")  // If only the message is empty.
				{
					messageBuilder.Append($" {category}{EntrySeparator}");
				}
				else if (message != "" && category == "") // If only the category is empty.
				{
					messageBuilder.Append($": {message}{EntrySeparator}");
				}
				else // if (message == "" && category == "") // If both empty. It probably never happens though.
				{
					messageBuilder.Append($"{EntrySeparator}");
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

								default: break; // Keep original color.
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
					LogDebug($"Logging failed: {ex}", $"{nameof(Logger)}.{nameof(Logging)}.{nameof(Logger)}");
				}
				// If logging the failure is successful then clear the failure counter.
				// If it's not the first time the logging failed, then we don't try to log logging failure, so clear the failure counter.
				Interlocked.Exchange(ref LoggingFailedCount, 0);
			}
		}

		private static void Log(LogLevel level, string message, Type category)
		{
			if (category is null)
			{
				Log(level, message, "");
			}
			else
			{
				Log(level, message, category.ToString());
			}
		}

		private static void Log<T>(LogLevel level, string message) => Log(level, message, typeof(T).Name);

		#endregion GeneralLoggingMethods

		#region ExceptionLoggingMethods

		private static void Log(Exception ex, LogLevel level, string category = "")
		{
			Log(level, ExceptionToStringHandleNull(ex), category);
		}

		private static void Log<T>(Exception ex, LogLevel level)
		{
			Log<T>(level, ExceptionToStringHandleNull(ex));
		}

		private static void Log(Exception ex, LogLevel level, Type category = null)
		{
			Log(level, ExceptionToStringHandleNull(ex), category);
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
		public static void LogTrace<T>(string message) => Log<T>(LogLevel.Trace, message);

		/// <summary>
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(string message, Type category) => Log(LogLevel.Trace, message, category);

		/// <summary>
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(string message, string category = "") => Log(LogLevel.Trace, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace<T>(Exception ex) => Log<T>(ex, LogLevel.Trace);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(Exception ex, Type category) => Log(ex, LogLevel.Trace, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Trace level.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogTrace(Exception ex, string category = "") => Log(ex, LogLevel.Trace, category);

		#endregion TraceLoggingMethods

		#region DebugLoggingMethods

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug<T>(string message) => Log<T>(LogLevel.Debug, message);

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug(string message, Type category) => Log(LogLevel.Debug, message, category);

		/// <summary>
		/// For information that has short-term usefulness during development and debugging.
		/// Example: "Entering method Configure with flag set to true."
		/// You typically would not enable Debug level logs in production unless you are troubleshooting, due to the high volume of logs.
		/// </summary>
		public static void LogDebug(string message, string category = "") => Log(LogLevel.Debug, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Debug level, if only Debug level logging is set.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogDebug<T>(Exception ex) => Log<T>(ex, LogLevel.Debug);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Debug level, if only Debug level logging is set.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogDebug(Exception ex, Type category = null) => Log(ex, LogLevel.Debug, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Debug level.
		///
		/// For information that is valuable only to a developer debugging an issue.
		/// These messages may contain sensitive application data and so should not be enabled in a production environment.
		/// Example: "Credentials: {"User":"someuser", "Password":"P@ssword"}"
		/// </summary>
		public static void LogDebug(Exception ex, string category = "") => Log(ex, LogLevel.Debug, category);

		#endregion DebugLoggingMethods

		#region InfoLoggingMethods

		/// <summary>
		/// Logs software start with category InstanceGuid and insert three newlines.
		/// </summary>
		/// <param name="appName">The name of the app.</param>
		public static void LogStarting(string appName) => Log(LogLevel.Info, $"{appName} is starting...", category: InstanceGuid.ToString(), additionalEntrySeparators: 3, additionalEntrySeparatorsLogFileOnlyMode: true);

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo<T>(string message) => Log<T>(LogLevel.Info, message);

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(string message, Type category) => Log(LogLevel.Info, message, category);

		/// <summary>
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(string message, string category = "") => Log(LogLevel.Info, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Info level.
		///
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo<T>(Exception ex) => Log<T>(ex, LogLevel.Info);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Info level.
		///
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// </summary>
		public static void LogInfo(Exception ex, Type category = null) => Log(ex, LogLevel.Info, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Info level.
		///
		/// For tracking the general flow of the application.
		/// These logs typically have some long-term value.
		/// </summary>
		public static void LogInfo(Exception ex, string category = "") => Log(ex, LogLevel.Info, category);

		#endregion InfoLoggingMethods

		#region WarningLoggingMethods

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning<T>(string message) => Log<T>(LogLevel.Warning, message);

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(string message, Type category) => Log(LogLevel.Warning, message, category);

		/// <summary>
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(string message, string category = "") => Log(LogLevel.Warning, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Warning level.
		///
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning<T>(Exception ex) => Log<T>(ex, LogLevel.Warning);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Warning level.
		///
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(Exception ex, Type category = null) => Log(ex, LogLevel.Warning, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Warning level.
		///
		/// For abnormal or unexpected events in the application flow.
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// Example: "FileNotFoundException for file quotes.txt."
		/// </summary>
		public static void LogWarning(Exception ex, string category = "") => Log(ex, LogLevel.Warning, category);

		#endregion WarningLoggingMethods

		#region ErrorLoggingMethods

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError<T>(string message) => Log<T>(LogLevel.Error, message);

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(string message, Type category) => Log(LogLevel.Error, message, category);

		/// <summary>
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(string message, string category = "") => Log(LogLevel.Error, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Error level.
		///
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError<T>(Exception ex) => Log<T>(ex, LogLevel.Error);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Error level.
		///
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(Exception ex, Type category = null) => Log(ex, LogLevel.Error, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.ToString() at Error level.
		///
		/// For errors and exceptions that cannot be handled.
		/// These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.
		/// Example log message: "Cannot insert record due to duplicate key violation."
		/// </summary>
		public static void LogError(Exception ex, string category = "") => Log(ex, LogLevel.Error, category);

		#endregion ErrorLoggingMethods

		#region CriticalLoggingMethods

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical<T>(string message) => Log<T>(LogLevel.Critical, message);

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(string message, Type category) => Log(LogLevel.Critical, message, category);

		/// <summary>
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(string message, string category = "") => Log(LogLevel.Critical, message, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level.
		///
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical<T>(Exception ex) => Log<T>(ex, LogLevel.Critical);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level.
		///
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(Exception ex, Type category = null) => Log(ex, LogLevel.Critical, category);

		/// <summary>
		/// Logs the <paramref name="ex"/>.Message at Critical level.
		///
		/// For failures that require immediate attention.
		/// Examples: data loss scenarios, out of disk space.
		/// </summary>
		public static void LogCritical(Exception ex, string category = "") => Log(ex, LogLevel.Critical, category);

		#endregion CriticalLoggingMethods

		#endregion LoggingMethods
	}
}
