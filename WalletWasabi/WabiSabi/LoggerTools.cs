using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi;

public static class LoggerTools
{
	public static void Log(this Round round, LogLevel logLevel, string logMessage)
	{
		var roundState = RoundState.FromRound(round);
		roundState.Log(logLevel, logMessage);
	}

	public static void LogInfo(this Round round, string logMessage)
	{
		Log(round, LogLevel.Info, logMessage);
	}

	public static void LogWarning(this Round round, string logMessage)
	{
		Log(round, LogLevel.Warning, logMessage);
	}

	public static void LogError(this Round round, string logMessage)
	{
		Log(round, LogLevel.Error, logMessage);
	}

	public static void Log(this RoundState roundState, LogLevel logLevel, string logMessage)
	{
		string round = roundState.IsBlame ? "Blame Round" : "Round";

		Logger.Log(logLevel, $"{round} ({roundState.Id}): {logMessage}");
	}

	public static void LogInfo(this RoundState roundState, string logMessage)
	{
		Log(roundState, LogLevel.Info, logMessage);
	}

	public static void LogDebug(this RoundState roundState, string logMessage)
	{
		Log(roundState, LogLevel.Debug, logMessage);
	}

	public static void Log(this IWallet wallet, LogLevel logLevel, string logMessage)
	{
		Logger.Log(logLevel, $"Wallet ({wallet.WalletName}): {logMessage}");
	}

	public static void LogInfo(this IWallet wallet, string logMessage)
	{
		Log(wallet, LogLevel.Info, logMessage);
	}

	public static void LogDebug(this IWallet wallet, string logMessage)
	{
		Log(wallet, LogLevel.Debug, logMessage);
	}

	public static void LogWarning(this IWallet wallet, string logMessage)
	{
		Log(wallet, LogLevel.Warning, logMessage);
	}

	public static void LogError(this IWallet wallet, string logMessage)
	{
		Log(wallet, LogLevel.Error, logMessage);
	}

	public static void LogTrace(this IWallet wallet, string logMessage)
	{
		Log(wallet, LogLevel.Trace, logMessage);
	}
}
