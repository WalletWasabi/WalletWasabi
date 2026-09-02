namespace WalletWasabi.IntegrationTests;

using System.Runtime.CompilerServices;

public static class ModuleInitializer
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		// Make sure that WalletWasabi.ModuleInitializer is initialized before running the tests.
		_ = typeof(WalletWasabi.ModuleInitializer);
	}
}
