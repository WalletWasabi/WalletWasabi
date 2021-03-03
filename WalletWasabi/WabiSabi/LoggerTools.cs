using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi
{
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
	}
}
