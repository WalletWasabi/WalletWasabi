namespace WalletWasabi.Daemon;

using System.Runtime.CompilerServices;
using WalletWasabi.Helpers;

class ModuleInitializer
{
	[ModuleInitializer]
	internal static void PatchTestNet()
	{
		NBitcoinHelpers.PatchTestNet();
	}
}
