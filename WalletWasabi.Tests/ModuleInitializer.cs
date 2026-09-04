using System.IO;
using System.Runtime.CompilerServices;
using WalletWasabi.Logging;
using WalletWasabi.Tests.Helpers;

namespace WalletWasabi.Tests;

public static class ModuleInitializer
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		// Make sure that WalletWasabi.ModuleInitializer is initialized before running the tests.
		RuntimeHelpers.RunClassConstructor(typeof(WalletWasabi.ModuleInitializer).TypeHandle);

		Logger.Configure(Path.Combine(Common.DataDir, "Logs.txt"), LogLevel.Info, [LogMode.Debug, LogMode.File]);
	}
}
