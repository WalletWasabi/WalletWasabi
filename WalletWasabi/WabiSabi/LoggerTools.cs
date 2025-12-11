using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi;

public static class LoggerTools
{
	public static void Log(this Round round, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		var roundState = RoundState.FromRound(round);
		roundState.Log(logLevel, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void Log(this RoundState roundState, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		string round = roundState.IsBlame ? "Blame Round" : "Round";

		Logger.Log(logLevel, $"{round} ({roundState.Id}): {logMessage}", callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this RoundState roundState, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}

	public static void LogDebug(this RoundState roundState, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState, LogLevel.Debug, logMessage, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
	}
}

