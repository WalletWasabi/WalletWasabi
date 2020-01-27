using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using WalletWasabi.Helpers;

namespace WalletWasabi.Logging
{
	public static class TempLogger
	{
		#region PropertiesAndMembers

		private static readonly object Lock = new object();

		public static string FilePath { get; private set; } = "TempLog.txt";

		public static string EntrySeparator { get; private set; } = Environment.NewLine;

		private static int LoggingFailedCount = 0;

		/// <summary>
		/// KB
		/// </summary>
		public static long MaximumLogFileSize { get; private set; } = 100_000; // approx 100 MB

		#endregion PropertiesAndMembers

		#region Initializers

		/// <summary>
		/// Initializes the Logger with default values.
		/// If RELEASE then minlevel is info, and logs only to file.
		/// If DEBUG then minlevel is debug, and logs to file, debug and console.
		/// </summary>
		public static void InitializeDefaults(string filePath)
		{
			SetFilePath(filePath);
		}

		public static void SetFilePath(string filePath) => FilePath = Guard.NotNullOrEmptyOrWhitespace(nameof(filePath), filePath, trim: true);

		public static void SetEntrySeparator(string entrySeparator) => EntrySeparator = Guard.NotNull(nameof(entrySeparator), entrySeparator);

		/// <summary>
		/// KB
		/// </summary>
		public static void SetMaximumLogFileSize(long sizeInKb) => MaximumLogFileSize = sizeInKb;

		#endregion Initializers

		#region LoggingMethods

		#region GeneralLoggingMethods

		public static void Log(string message, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			try
			{
				message = Guard.Correct(message);
				var category = string.IsNullOrWhiteSpace(callerFilePath) ? "" : $"{EnvironmentHelpers.ExtractFileName(callerFilePath)} ({callerLineNumber})";

				var messageBuilder = new StringBuilder();
				messageBuilder.Append($"{DateTime.UtcNow.ToLocalTime():yyyy-MM-dd HH:mm:ss}\t");

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

				var finalFileMessage = messageBuilder.ToString();

				lock (Lock)
				{
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
					Log($"Logging failed: {ex}");
				}
				// If logging the failure is successful then clear the failure counter.
				// If it's not the first time the logging failed, then we do not try to log logging failure, so clear the failure counter.
				Interlocked.Exchange(ref LoggingFailedCount, 0);
			}
		}

		#endregion GeneralLoggingMethods

		#region ExceptionLoggingMethods

		public static void Log(Exception ex, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
		{
			Log(ExceptionToStringHandleNull(ex), callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
		}

		private static string ExceptionToStringHandleNull(Exception ex)
		{
			return ex?.ToString() ?? "Exception was null.";
		}

		#endregion ExceptionLoggingMethods

		#endregion LoggingMethods
	}
}
