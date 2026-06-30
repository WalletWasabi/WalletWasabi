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
		Logger.SetFilePath(Path.Combine(Common.DataDir, "Logs.txt"));
		Logger.SetMinimumLevel(LogLevel.Info);
		Logger.SetModes(LogMode.Debug, LogMode.File);
	}
}
