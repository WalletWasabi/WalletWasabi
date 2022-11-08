using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging;

public class LoggerImpl
{
	public static string EntrySeparator { get; } = Environment.NewLine;

	public LoggerImpl(bool isEnabled, string filePath, LogLevel minimumLevel, IReadOnlySet<LogMode> modes, long maximumLogFileSize)
	{
		IsEnabled = isEnabled;
		FilePath = filePath;
		MinimumLevel = minimumLevel;
		Modes = ImmutableHashSet.Create(modes.ToArray());
		MaximumLogFileSize = maximumLogFileSize;

		Writer = new(filePath, append: true);

		FileSize = (File.Exists(FilePath))
			? new FileInfo(FilePath).Length
			: 0;
	}

	private object Lock { get; } = new();
	public bool IsEnabled { get; }
	public string FilePath { get; init; }

	public long MaximumLogFileSize { get; }
	public LogLevel MinimumLevel { get; init; }

	public IImmutableSet<LogMode> Modes { get; init; }

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private StreamWriter? Writer { get; set; }

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private long FileSize { get; set; }

	private bool IsEnabledOnLevel(LogLevel level)
		=> IsEnabled && Modes.Count > 0 && level >= MinimumLevel;

	public void Log(LogLevel level, string message, Exception? ex = null, int additionalEntrySeparators = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		if (!IsEnabledOnLevel(level))
		{
			return;
		}

		LogCore(level, message, ex, additionalEntrySeparators, callerFilePath, callerMemberName, callerLineNumber);
	}

	public void Log(LogLevel level, DefaultInterpolatedStringHandler builder, Exception? ex = null, int additionalEntrySeparators = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		if (!IsEnabledOnLevel(level))
		{
			return;
		}

		LogCore(level, builder.ToStringAndClear(), ex, additionalEntrySeparators, callerFilePath, callerMemberName, callerLineNumber);
	}

	private void LogCore(LogLevel level, string message, Exception? exception = null, int additionalEntrySeparators = 0, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		try
		{
			string category = string.IsNullOrWhiteSpace(callerFilePath)
				? ""
				: $"{EnvironmentHelpers.ExtractFileName(callerFilePath)}.{callerMemberName} ({callerLineNumber})";

			var messageBuilder = new StringBuilder();
			messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {level.ToString().ToUpperInvariant()}\t");

			if (message.Length == 0)
			{
				if (category.Length == 0) // If both empty. It probably never happens though.
				{
					messageBuilder.Append(EntrySeparator);
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
					if (exception is not null)
					{
						messageBuilder.Append($"{message} Exception: {exception}{EntrySeparator}");
					}
					else
					{
						messageBuilder.Append($"{message}{EntrySeparator}");
					}
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

				if (MaximumLogFileSize > 0 && FileSize > 1000 * MaximumLogFileSize)
				{
					FlushAndCloseNoLock();
					File.Delete(FilePath);

					Writer = new(FilePath, append: true);
					FileSize = finalFileMessage.Length;
				}
				else
				{
					FileSize += finalFileMessage.Length;
				}

				if (Writer is { } writer)
				{
					writer.Write(finalFileMessage);
				}
			}
		}
		catch
		{
			// Ignore.
		}
	}

	public void FlushAndClose()
	{
		lock (Lock)
		{
			FlushAndCloseNoLock();
		}
	}

	/// <remarks>Guarded by <see cref="Lock"/>.</remarks>
	private void FlushAndCloseNoLock()
	{
		if (Writer is { } writer)
		{
			writer.Flush();
			writer.Dispose();
			Writer = null;
		}
	}
}
