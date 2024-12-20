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

		var testnet4 = networks[new ChainName("testnet4")];

		// Replaces testnet by testnet4 network
		networks[new ChainName("testnet")] = testnet4;

		var otherAliasesField = typeof(Network)
			.GetField("_OtherAliases", BindingFlags.NonPublic | BindingFlags.Static);

		var otherAliases = otherAliasesField!.GetValue(null) as ConcurrentDictionary<string, Network>;
		otherAliases["test"] = testnet4;
		otherAliases["testnet"] = testnet4;
	}
}
