using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging;

public static class Logger
{
	private static readonly object Lock = new();
	private static LogLevel MinimumLevel { get; set; } = LogLevel.Critical;
	private static HashSet<LogMode> Modes { get; } = new();
	public static string FilePath { get; private set; } = "Log.txt";
	public static string EntrySeparator { get; } = Environment.NewLine;
	private static Guid InstanceGuid { get; } = Guid.NewGuid();
	private static long MaximumLogFileSizeBytes { get; set; } = 10_000_000;
	private static long LogFileSizeBytes { get; set; }


	public static void InitializeDefaults(string filePath, LogLevel? logLevel = null, LogMode[]? logModes = null)
	{
		SetFilePath(filePath);

#if RELEASE
		logLevel ??= LogLevel.Info;
		logModes ??= [LogMode.Console, LogMode.File];
#else
		logLevel ??= LogLevel.Debug;
		logModes ??= [LogMode.Debug, LogMode.Console, LogMode.File];
#endif

		SetMinimumLevel(logLevel.Value);
		SetModes(logModes);

		lock (Lock)
		{
			if (MinimumLevel == LogLevel.Trace)
			{
				MaximumLogFileSizeBytes = 0;
			}
		}
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

	public static void SetFilePath(string filePath)
	{
		FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);
		IoHelpers.EnsureContainingDirectoryExists(FilePath);

		if (File.Exists(FilePath))
		{
			lock (Lock)
			{
				LogFileSizeBytes = new FileInfo(FilePath).Length;
			}
		}
	}

	private static string ShortLevel(LogLevel level) =>
		level switch
		{
			LogLevel.Trace => "TRC",
			LogLevel.Debug => "DBG",
			LogLevel.Info => "INF",
			LogLevel.Warning => "WRN",
			LogLevel.Error => "ERR",
			LogLevel.Critical => "CRT",
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
		};

	public static void Log(LogLevel level, string message, int additionalEntrySeparators = 0, bool additionalEntrySeparatorsLogFileOnlyMode = true, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
			if (Modes.Count == 0)
			{
				return;
			}

			if (level < MinimumLevel)
			{
				return;
			}

			message = Guard.Correct(message);
			var category = string.IsNullOrWhiteSpace(callerFilePath) ? "" : $"{callerFilePath}:{callerLineNumber}";

			var messageBuilder = new StringBuilder();
			messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId,2}] {ShortLevel(level)} | ");

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
					if (category.Length > 40)
					{
						category = $"..{category.Substring(category.Length - 38)}";
					}
					messageBuilder.Append($"{category,-40} | {message}{EntrySeparator}");
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

				if (MaximumLogFileSizeBytes > 0)
				{
					// Simplification here is that: 1 character ~ 1 byte.
					LogFileSizeBytes += finalFileMessage.Length;

					if (LogFileSizeBytes > MaximumLogFileSizeBytes)
					{
						LogFileSizeBytes = 0;
						File.Delete(FilePath);
					}
				}

				File.AppendAllText(FilePath, finalFileMessage);
			}
	}

	private static void Log(string message, Exception ex, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(level, message: $"{message} Exception: {ex}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	private static void Log(Exception exception, LogLevel level, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(level, exception.ToString(), callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogTrace(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Trace, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogTrace(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Trace, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogDebug(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Debug, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogDebug(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Debug, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogSoftwareStarted(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(LogLevel.Info, $"{appName} started ({InstanceGuid}).", additionalEntrySeparators: 3, additionalEntrySeparatorsLogFileOnlyMode: true, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogSoftwareStopped(string appName, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(LogLevel.Info, $"{appName} stopped gracefully ({InstanceGuid}).", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogInfo(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Info, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Info, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogWarning(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Warning, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Warning, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogError(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Error, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogError(string message, Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
		=> Log(message, exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Error, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogCritical(string message, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(LogLevel.Critical, message, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);

	public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1) => Log(exception, LogLevel.Critical, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
}
