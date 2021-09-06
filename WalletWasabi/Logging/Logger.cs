using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging
{
	/// <summary>
	/// Logging class.
	///
	/// <list type="bullet">
	/// <item>Logger is enabled by default but no <see cref="Modes"/> are set by default, so the logger does not log by default.</item>
	/// <item>Only <see cref="LogLevel.Critical"/> messages are logged unless set otherwise.</item>
	/// <item>The logger is thread-safe.</item>
	/// </list>
	/// </summary>
	public static class Logger
	{
		#region PropertiesAndMembers

		private static readonly object Lock = new();

		private static long On = 1;

		private static int LoggingFailedCount = 0;

		private static LogLevel MinimumLevel { get; set; } = LogLevel.Critical;

		private static HashSet<LogMode> Modes { get; } = new HashSet<LogMode>();

		public static string FilePath { get; private set; } = "Log.txt";

		public static string EntrySeparator { get; private set; } = Environment.NewLine;

		/// <summary>
		/// Gets the GUID instance.
		///
		/// <para>You can use it to identify which software instance created a log entry. It gets created automatically, but you have to use it manually.</para>
		/// </summary>
		private static Guid InstanceGuid { get; } = Guid.NewGuid();

		/// <summary>
		/// Gets or sets the maximum log file size in KB.
		/// </summary>
		/// <remarks>Default value is approximately 10 MB.</remarks>
		private static long MaximumLogFileSize { get; set; } = 10_000;

		#endregion PropertiesAndMembers

		#region Initializers

		/// <summary>
		/// Initializes the logger with default values.
		/// <para>
		/// Default values are set as follows:
		/// <list type="bullet">
		/// <item>For RELEASE mode: <see cref="MinimumLevel"/> is set to <see cref="LogLevel.Info"/>, and logs only to file.</item>
		/// <item>For DEBUG mode: <see cref="MinimumLevel"/> is set to <see cref="LogLevel.Debug"/>, and logs to file, debug and console.</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="logLevel">Use <c>null</c> to use default <see cref="LogLevel"/> or a custom value to force non-default <see cref="LogLevel"/>.</param>
		public static void InitializeDefaults(string filePath, LogLevel? logLevel = null)
		{
			SetFilePath(filePath);

#if RELEASE
			SetMinimumLevel(logLevel ??= LogLevel.Info);
			SetModes(LogMode.Console, LogMode.File);

#else
			SetMinimumLevel(logLevel ??= LogLevel.Debug);
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

		public static void Log(LogLevel level, string message, int additionalEntrySeparators = 0, bool additionalEntrySeparatorsLogFileOnlyMode = true, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
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

				message = Guard.Correct(message);
				var category = string.IsNullOrWhiteSpace(callerFilePath) ? "" : $"{EnvironmentHelpers.ExtractFileName(callerFilePath)}:{callerMemberName} ({callerLineNumber})";

				var messageBuilder = new StringBuilder();
				messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Thread.CurrentThread.ManagedThreadId}] {level.ToString().ToUpperInvariant()}\t");

				if (message.Length == 0)
				{
					if (category.Length == 0) // If both empty. It probably never happens though.
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
					if (category.Length == 0) // If only the category is empty.
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

					if (Modes.Contains(LogMode.Debug))
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

		#endregion GeneralLoggingMethods

		#region ExceptionLoggingMethods

		/// <summary>
		/// Logs user message concatenated with exception string.
		/// </summary>
		private static void Log(string message, Exception ex, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			Log(level, message: $"{message} Exception: {ex}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
		}

		/// <summary>
		/// Logs exception string without any user message.
		/// </summary>
		private static void Log(Exception exception, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			Log(level, exception.ToString(), callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
		}

		#endregion ExceptionLoggingMethods

		#region TraceLoggingMethods

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Trace"/> level.
		///
		/// <para>For information that is valuable only to a developer debugging an issue.</para>
		/// </summary>
		/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
		/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
		public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Trace"/> level.
		///
		/// <para>For information that is valuable only to a developer debugging an issue.</para>
		/// </summary>
		/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
		/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
		public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Trace"/> level.
		///
		/// <para>For information that is valuable only to a developer debugging an issue.</para>
		/// </summary>
		/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
		/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
		public static void LogTrace(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(message, exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion TraceLoggingMethods

		#region DebugLoggingMethods

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Debug"/> level.
		///
		/// <para>For information that has short-term usefulness during development and debugging.</para>
		/// </summary>
		/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
		/// <example>For example: <c>Entering method Configure with flag set to true.</c></example>
		public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Debug"/> level.
		///
		/// <para>For information that is valuable only to a developer debugging an issue.</para>
		/// </summary>
		/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
		/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
		public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Debug"/> level.
		///
		/// <para>For information that has short-term usefulness during development and debugging.</para>
		/// </summary>
		/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
		public static void LogDebug(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(message, exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion DebugLoggingMethods

		#region InfoLoggingMethods

		/// <summary>
		/// Logs special event: Software has started. Add also <see cref="InstanceGuid"/> identifier and insert three newlines to increase log readability.
		/// </summary>
		/// <param name="appName">Name of the application.</param>
		public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(LogLevel.Info, $"{appName} started ({InstanceGuid}).", additionalEntrySeparators: 3, additionalEntrySeparatorsLogFileOnlyMode: true, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs special event: Software has stopped. Add also <see cref="InstanceGuid"/> identifier.
		/// </summary>
		/// <param name="appName">Name of the application.</param>
		public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(LogLevel.Info, $"{appName} stopped gracefully ({InstanceGuid}).", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Info"/> level.
		///
		/// <para>For tracking the general flow of the application.</para>
		/// <remarks>These logs typically have some long-term value.</remarks>
		/// <example>"Request received for path /api/my-controller"</example>
		/// </summary>
		public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Info, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Info"/> level.
		///
		/// <para>For tracking the general flow of the application.</para>
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Info, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Info"/> level.
		///
		/// <para>For tracking the general flow of the application.</para>
		/// These logs typically have some long-term value.
		/// Example: "Request received for path /api/my-controller"
		/// </summary>
		public static void LogInfo(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(message, exception, LogLevel.Info, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion InfoLoggingMethods

		#region WarningLoggingMethods

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Warning"/> level.
		///
		/// <para>For abnormal or unexpected events in the application flow.</para>
		/// <remarks>
		/// These may include errors or other conditions that do not cause the application to stop, but which may need to be investigated.
		/// Handled exceptions are a common place to use the Warning log level.
		/// </remarks>
		/// <example>"FileNotFoundException for file quotes.txt."</example>
		/// </summary>
		public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Warning"/> level.
		///
		/// <para>For abnormal or unexpected events in the application flow.</para>
		/// </summary>
		/// <remarks>
		/// <para>Includes situations when errors or other conditions occur that do not cause the application to stop, but which may need to be investigated.</para>
		/// <para>Handled exceptions are a common place to use the <see cref="LogLevel.Warning"/> log level.</para>
		/// </remarks>
		/// <example>For example: <c>FileNotFoundException for file quotes.txt.</c></example>
		public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Warning, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion WarningLoggingMethods

		#region ErrorLoggingMethods

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Error"/> level.
		///
		/// <para>For errors and exceptions that cannot be handled.</para>
		/// </summary>
		/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
		/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
		public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Error"/> level.
		///
		/// <para>For errors and exceptions that cannot be handled.</para>
		/// </summary>
		/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
		/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
		public static void LogError(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
			=> Log(message, exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Error"/> level.
		///
		/// <para>For errors and exceptions that cannot be handled.</para>
		/// </summary>
		/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
		/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
		public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion ErrorLoggingMethods

		#region CriticalLoggingMethods

		/// <summary>
		/// Logs a string message at <see cref="LogLevel.Critical"/> level.
		///
		/// <para>For failures that require immediate attention.</para>
		/// </summary>
		/// <example>Data loss scenarios, out of disk space.</example>
		public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Critical, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		/// <summary>
		/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Critical"/> level.
		///
		/// <para>For failures that require immediate attention.</para>
		/// </summary>
		/// <example>Examples: Data loss scenarios, out of disk space, etc.</example>
		public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Critical, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

		#endregion CriticalLoggingMethods

		#endregion LoggingMethods
	}
}
