using System.Runtime.CompilerServices;
using System.Threading;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Wallets;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.Logging;

public static class Logger
{
	private static readonly Lock ConfigLock = new();

	private static LogLevel LogLevel = LogLevel.Info;
	private static LogMode[] LogModes = [LogMode.Console, LogMode.File];

	public static string FilePath
	{
		get
		{
			lock (ConfigLock)
			{
				return field;
			}
		}
		private set
		{
			lock (ConfigLock)
			{
				field = value;
			}
		}
	} = "Logs.txt";

	private static Lazy<LoggerCore> Core { get; } = new(() => new LoggerCore(FilePath, LogLevel, LogModes), isThreadSafe: true);

	/// <summary>
	/// Configure the logger. Must be called before any logging occurs.
	/// </summary>
	public static void Configure(string? filePath = null, LogLevel? logLevel = null, LogMode[]? logModes = null)
	{
		lock (ConfigLock)
		{
			if (Core.IsValueCreated)
			{
				throw new InvalidOperationException("Logger has already been initialized and cannot be reconfigured.");
			}

			if (logLevel.HasValue)
			{
				LogLevel = logLevel.Value;
			}

			if (filePath is not null)
			{
				FilePath = filePath;
			}

			if (logModes is not null)
			{
				LogModes = logModes;
			}
		}
	}

	public static void LogTrace(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Trace, message, ctx, callerFilePath, callerLineNumber);

	public static void LogDebug(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Debug, message, ctx, callerFilePath, callerLineNumber);

	public static void LogInfo(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Info, message, ctx, callerFilePath, callerLineNumber);

	public static void LogWarning(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Warning, message, ctx, callerFilePath, callerLineNumber);

	public static void LogError(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Error, message, ctx, callerFilePath, callerLineNumber);

	public static void LogCritical(string message, object? ctx = null, [CallerFilePath] string callerFilePath = "",
		[CallerLineNumber] int callerLineNumber = -1) =>
		Core.Value.Log(LogLevel.Critical, message, ctx, callerFilePath, callerLineNumber);

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
}

public static class LoggerTools
{
	private static string ShortString(object o)
	{
		var s = o.ToString();
		return s?.Length switch
		{
			null => "",
			<= 8 => s,
			_ => s[0..8]
		};
	}

	public static string FormatLog(string msg, string ctx) => $"{ctx} {msg}";
	public static string FormatLog(string msg, Round round) => FormatLog(msg, $"Round {ShortString(round.Id)}");
	public static string FormatLog(string msg, RoundState round) => FormatLog(msg, $"Round {ShortString(round.Id)}");
	public static string FormatLog(string msg, AliceClient aliceClient) =>
		FormatLog(msg, $"Round {ShortString(aliceClient.RoundId)} Alice {ShortString(aliceClient.AliceId)} EffValue {aliceClient.EffectiveValue}");
	public static string FormatLog(string msg, Wallet wallet) => FormatLog(msg, $"Wallet '{wallet.WalletName}'");
}
