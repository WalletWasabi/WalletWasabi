namespace WalletWasabi.Tests;

using System.IO;
using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;

public class ModuleInitializer
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		Logger.Configure(Path.Combine(Common.DataDir, "Logs.txt"), LogLevel.Info, [LogMode.Debug, LogMode.File]);
	}
}
