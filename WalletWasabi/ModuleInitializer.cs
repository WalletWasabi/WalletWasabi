namespace WalletWasabi;
using System.Collections.Concurrent;
using System.Reflection;
using NBitcoin;
using System.Runtime.CompilerServices;

class ModuleInitializer
{
	[ModuleInitializer]
	internal static void PatchTestNet()
	{
		// This is necessary to force the static members to be initialized
		RuntimeHelpers.RunClassConstructor(typeof(Network).TypeHandle);

		// Access the Bitcoin.Instance
		var bitcoinInstance = Bitcoin.Instance;

		// Get the private field `_Networks` using reflection
		var networksField = bitcoinInstance
			.GetType()
			.GetField("_Networks", BindingFlags.NonPublic | BindingFlags.Instance);

		// Get the internal dictionary
		var networks = networksField!.GetValue(bitcoinInstance) as ConcurrentDictionary<ChainName, Network>;

		// Replaces testnet by testnet4 network
		networks[new ChainName("testnet")] = networks[new ChainName("testnet4")];
	}
}
