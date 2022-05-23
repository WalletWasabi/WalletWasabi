using NBitcoin;
using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi;

public static class LoggerTools
{
	public static void Log(this Round round, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		var roundState = RoundState.FromRound(round);
		roundState.Log(logLevel, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this Round round, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(round, LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void Log(this RoundState roundState, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		string round = roundState.BlameOf == uint256.Zero ? "Round" : "Blame Round";

		Logger.Log(logLevel, $"{round} ({roundState.Id}): {logMessage}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this RoundState roundState, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogDebug(this RoundState roundState, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(roundState, LogLevel.Debug, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void Log(this Wallet wallet, LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Logger.Log(logLevel, $"Wallet ({wallet.WalletName}): {logMessage}", callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogInfo(this Wallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Info, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogDebug(this Wallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Debug, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogWarning(this Wallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Warning, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}

	public static void LogError(this Wallet wallet, string logMessage, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
	{
		Log(wallet, LogLevel.Error, logMessage, callerFilePath: callerFilePath, callerMemberName: callerMemberName, callerLineNumber: callerLineNumber);
	}
}
