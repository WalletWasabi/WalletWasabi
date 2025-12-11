using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Logging;

public static class Logger
{
	private static readonly object Lock = new();
	private static LogLevel MinimumLevel { get; set; } = LogLevel.Critical;
	private static HashSet<LogMode> Modes { get; } = new();
	public static string FilePath { get; private set; } = "Log.txt";
	public static string EntrySeparator { get; } = Environment.NewLine;
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
		Modes.Clear();
		Modes.UnionWith(modes);
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

	private static string GetShortNameLevel(LogLevel level) =>
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

	private static ConsoleColor GetConsoleColor(LogLevel level) =>
		level switch
		{
			LogLevel.Warning => ConsoleColor.Yellow,
			LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
			_ => Console.ForegroundColor
		};

	private static string FormatCategory(string callerFilePath, int callerLineNumber)
	{
		if (string.IsNullOrWhiteSpace(callerFilePath))
		{
			return "";
		}

		var filePath = Path.GetFileName(callerFilePath);
		var category = $"{filePath}:{callerLineNumber}";
		return category.Length > 30 ? $"..{category.Substring(category.Length - 28)}" : category;
	}

	private static void AppendMessageContent(StringBuilder builder, string message, string category)
	{
		if (message.Length == 0 && category.Length == 0)
		{
			return;
		}

		if (category.Length == 0)
		{
			builder.Append(message);
		}
		else if (message.Length == 0)
		{
			builder.Append(category);
		}
		else
		{
			builder.Append($"{category,-30} | {message}");
		}
	}

	private static string ContextToString(object? ctx) =>
		ctx switch
		{
			not null => ctx.ToString()!,
			_ => ""
		};

	public static void Log(LogLevel level, string message, object? ctx = null, string callerFilePath = "",
		int callerLineNumber = -1)
	{
		if (Modes.Count == 0)
		{
			return;
		}

		if (level < MinimumLevel)
		{
			return;
		}

		var category = FormatCategory(callerFilePath, callerLineNumber);

		var messageBuilder = new StringBuilder();
		messageBuilder.Append(
			$"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId,2}] {GetShortNameLevel(level)} | ");

		AppendMessageContent(messageBuilder, message, category);
		messageBuilder.Append(EntrySeparator);

		if (ctx is not null)
		{
			messageBuilder.AppendLine(ContextToString(ctx));
		}

		var finalMessage = messageBuilder.ToString();

		var finalFileMessage = messageBuilder.ToString();
		lock (Lock)
		{
			if (Modes.Contains(LogMode.Console))
			{
				lock (Console.Out)
				{
					Console.ForegroundColor = GetConsoleColor(level);
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

	public static void LogTrace(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Trace, message, ctx, callerFilePath, callerLineNumber);

	public static void LogDebug(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Debug, message, ctx, callerFilePath, callerLineNumber);

	public static void LogInfo(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Info, message, ctx, callerFilePath, callerLineNumber);

	public static void LogWarning(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Warning, message, ctx, callerFilePath, callerLineNumber);

	public static void LogError(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Error, message, ctx, callerFilePath, callerLineNumber);

	public static void LogCritical(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Log(LogLevel.Critical, message, ctx, callerFilePath, callerLineNumber);

	// ReSharper disable ExplicitCallerInfoArgument
	public static void LogTrace(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogTrace(exception.ToString(), null, callerFilePath, callerLineNumber);

	public static void LogDebug(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogDebug(exception.ToString(), null, callerFilePath, callerLineNumber);

	public static void LogInfo(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogInfo(exception.ToString(), null, callerFilePath, callerLineNumber);

	public static void LogWarning(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogWarning(exception.ToString(), null, callerFilePath, callerLineNumber);

	public static void LogError(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogError(exception.ToString(), null, callerFilePath, callerLineNumber);

	public static void LogCritical(Exception exception, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		LogCritical(exception.ToString(), null, callerFilePath, callerLineNumber);
	// ReSharper restore ExplicitCallerInfoArgument
}

public static class LoggerTools
{
	private static string ShortString(object o) => o.ToString()?.Substring(0, 8) ?? "";
	public static string FormatLog(string msg, string ctx) => $"{ctx} {msg}";
	public static string FormatLog(string msg, Round round) => FormatLog(msg, $"Round {ShortString(round.Id)}");
	public static string FormatLog(string msg, RoundState round) => FormatLog(msg, $"Round {ShortString(round.Id)}");
	public static string FormatLog(string msg, AliceClient aliceClient) =>
		FormatLog(msg, $"Round {ShortString(aliceClient.RoundId)} Alice {ShortString(aliceClient.AliceId)} EffValue {aliceClient.EffectiveValue}");
	public static string FormatLog(string msg, IWallet wallet) => FormatLog(msg, $"Wallet '{wallet.WalletName}'");
}
