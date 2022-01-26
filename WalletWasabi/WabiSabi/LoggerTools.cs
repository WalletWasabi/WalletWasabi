using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi;

public static class LoggerTools
{
	public static void Log(this Round round, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Logger.Log(logLevel, $"Round ({round.Id}): {logMessage}", callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}
}
