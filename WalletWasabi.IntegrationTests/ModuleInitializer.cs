using System.Runtime.CompilerServices;

namespace WalletWasabi.IntegrationTests;

public static class ModuleInitializer
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		// Make sure that WalletWasabi.ModuleInitializer is initialized before running the tests.
		RuntimeHelpers.RunClassConstructor(typeof(WalletWasabi.ModuleInitializer).TypeHandle);
	}
}
