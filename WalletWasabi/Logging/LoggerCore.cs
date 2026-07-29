using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging;

public class LoggerCore
{
	private LogLevel MinimumLevel { get; }
	private ImmutableHashSet<LogMode> Modes { get; }
	public string FilePath { get; }
	public string EntrySeparator { get; } = Environment.NewLine;

	/// <summary>Maximum file size of the log file in bytes. 0 means there is no limit.</summary>
	private long MaximumLogFileSizeBytes { get; }

	/// <summary>Lock for console output, debug info output, file output to prevent concurrent attempts.</summary>
	private readonly Lock _lock = new();

	/// <summary>Current file size in bytes.</summary>
	/// <remarks>Guarded by <see cref="_lock"/>.</remarks>
	private long LogFileSizeBytes { get; set; }

	public LoggerCore(string filePath, LogLevel logLevel, LogMode[] logModes)
	{
		FilePath = filePath;
		MinimumLevel = logLevel;
		Modes = [.. logModes];
		MaximumLogFileSizeBytes = MinimumLevel == LogLevel.Trace ? 0 : 10_000_000;
		LogFileSizeBytes = File.Exists(FilePath) ? new FileInfo(FilePath).Length : 0;

		IoHelpers.EnsureContainingDirectoryExists(FilePath);
	}

	private string GetShortNameLevel(LogLevel level) =>
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

	private ConsoleColor GetConsoleColor(LogLevel level) =>
		level switch
		{
			LogLevel.Warning => ConsoleColor.Yellow,
			LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
			_ => Console.ForegroundColor
		};

	private string FormatCategory(string callerFilePath, int callerLineNumber)
	{
		if (string.IsNullOrWhiteSpace(callerFilePath))
		{
			return "";
		}

		var filePath = Path.GetFileName(callerFilePath);
		var category = $"{filePath}:{callerLineNumber}";
		return category.Length > 30 ? $"..{category[^28..]}" : category;
	}

	private void AppendMessageContent(StringBuilder builder, string message, string category)
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

	private string ContextToString(object? ctx) =>
		ctx switch
		{
			not null => ctx.ToString()!,
			_ => ""
		};

	public void Log(LogLevel level, string message, object? ctx = null, string callerFilePath = "", int callerLineNumber = -1)
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
		lock (_lock)
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
}
