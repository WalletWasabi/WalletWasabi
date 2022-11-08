using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WalletWasabi.Logging;

/// <summary>
/// Logging class.
/// </summary>
/// <remarks>The logger is thread-safe.</remarks>
public static class Logger
{
	private const string StandardExceptionMessage = "Exception occurred.";

#if RELEASE
	private static readonly LogLevel DefaultMinimumLogLevel = LogLevel.Info;
	private static readonly LogMode[] DefaultModes = new[] { LogMode.Console, LogMode.File };
#else
	private static readonly LogLevel DefaultMinimumLogLevel = LogLevel.Debug;
	private static readonly LogMode[] DefaultModes = new[] { LogMode.Debug, LogMode.Console, LogMode.File };
#endif

	private static Lazy<LoggerImpl> Implementation { get; set; } = new(() => Initialize(isEnabled: true, filePath: "File.logs"));

	/// <summary>
	/// Gets the GUID instance.
	///
	/// <para>You can use it to identify which software instance created a log entry. It gets created automatically, but you have to use it manually.</para>
	/// </summary>
	private static Guid InstanceGuid { get; } = Guid.NewGuid();

	public static string FilePath => Implementation.Value.FilePath;

	/// <summary>
	/// Initializes the logger.
	/// </summary>
	/// <remarks>Defaults are <see cref="DefaultMinimumLogLevel"/> and <see cref="DefaultModes"/>.</remarks>
	public static LoggerImpl Initialize(bool isEnabled, string filePath, LogLevel? minimumLogLevel = null, params LogMode[] modes)
	{
		minimumLogLevel ??= DefaultMinimumLogLevel;

		HashSet<LogMode> allowedModes = new();

		foreach (var mode in modes.Length > 0 ? modes : DefaultModes)
		{
			allowedModes.Add(mode);
		}

		long maximumLogFileSize = minimumLogLevel.Value == LogLevel.Trace ? 0 : 10_000;

		LoggerImpl impl = new(isEnabled, filePath, minimumLogLevel.Value, allowedModes, maximumLogFileSize);
		Implementation = new Lazy<LoggerImpl>(impl);

		return impl;
	}

	/// <summary>
	/// Logs a string message at the predefined level.
	/// </summary>
	public static void Log(LogLevel level, string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(level, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Trace"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <seealso cref="LogTrace(string, string, string, int)"/>
	public static void LogTrace(DefaultInterpolatedStringHandler message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Trace"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Trace, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <inheritdoc cref="LogTrace(string, string, string, int)"/>
	public static void LogTrace(DefaultInterpolatedStringHandler message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Trace, message, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);


	public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	/// <example>For example: <c>Entering method Configure with flag set to true.</c></example>
	public static void LogDebug(DefaultInterpolatedStringHandler message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that is valuable only to a developer debugging an issue.</para>
	/// </summary>
	/// <remarks>These messages may contain sensitive application data and so should not be enabled in a production environment.</remarks>
	/// <example>For example: <c>Credentials: {"User":"SomeUser", "Password":"P@ssword"}</c></example>
	public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Debug, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Debug"/> level.
	///
	/// <para>For information that has short-term usefulness during development and debugging.</para>
	/// </summary>
	/// <remarks>You typically would not enable <see cref="LogLevel.Debug"/> level in production unless you are troubleshooting, due to the high volume of generated logs.</remarks>
	public static void LogDebug(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Debug, message, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <inheritdoc cref="LogDebug(string, Exception, string, string, int)"/>
	public static void LogDebug(DefaultInterpolatedStringHandler message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Debug, message, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs special event: Software has started. Add also <see cref="InstanceGuid"/> identifier and insert three newlines to increase log readability.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Info, $"{appName} started ({InstanceGuid}).", additionalEntrySeparators: 3, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs special event: Software has stopped. Add also <see cref="InstanceGuid"/> identifier.
	/// </summary>
	/// <param name="appName">Name of the application.</param>
	public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Info, $"{appName} stopped gracefully ({InstanceGuid}).", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Info"/> level.
	///
	/// <para>For tracking the general flow of the application.</para>
	/// <remarks>These logs typically have some long-term value.</remarks>
	/// <example>"Request received for path /api/my-controller"</example>
	/// </summary>
	public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Info, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Info"/> level.
	///
	/// <para>For tracking the general flow of the application.</para>
	/// These logs typically have some long-term value.
	/// Example: "Request received for path /api/my-controller"
	/// </summary>
	public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Info, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <inheritdoc cref="LogInfo(string, string, string, int)"/>
	public static void LogInfo(DefaultInterpolatedStringHandler message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Info, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

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
	public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <inheritdoc cref="LogWarning(string, string, string, int)"/>
	public static void LogWarning(DefaultInterpolatedStringHandler message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

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
	public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Warning, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <inheritdoc cref="LogError(string, string, string, int)"/>
	public static void LogError(DefaultInterpolatedStringHandler message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogError(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Error, message, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs <paramref name="message"/> with <paramref name="exception"/> using <see cref="Exception.ToString()"/> concatenated to it at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(DefaultInterpolatedStringHandler message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Error, message, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Error"/> level.
	///
	/// <para>For errors and exceptions that cannot be handled.</para>
	/// </summary>
	/// <remarks>These messages indicate a failure in the current activity or operation (such as the current HTTP request), not an application-wide failure.</remarks>
	/// <example>Log message such as: "Cannot insert record due to duplicate key violation."</example>
	public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Error, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs a string message at <see cref="LogLevel.Critical"/> level.
	///
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Data loss scenarios, out of disk space.</example>
	public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Critical, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	/// <summary>
	/// Logs the <paramref name="exception"/> using <see cref="Exception.ToString()"/> at <see cref="LogLevel.Critical"/> level.
	///
	/// <para>For failures that require immediate attention.</para>
	/// </summary>
	/// <example>Examples: Data loss scenarios, out of disk space, etc.</example>
	public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Implementation.Value.Log(LogLevel.Critical, StandardExceptionMessage, exception, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
}
