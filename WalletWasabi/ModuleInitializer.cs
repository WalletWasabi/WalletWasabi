using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace WalletWasabi;

public static class ModuleInitializer
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries -- Moving this initializer to the apps did not work (https://github.com/WalletWasabi/WalletWasabi/pull/14364).
	[ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
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

		var testnet4 = networks![Network.TestNet4.ChainName];

		// Replaces TestNet by TestNet4 network
		networks[Network.TestNet.ChainName] = testnet4;

		var otherAliasesField = typeof(Network)
			.GetField("_OtherAliases", BindingFlags.NonPublic | BindingFlags.Static);

		var otherAliases = otherAliasesField!.GetValue(null) as ConcurrentDictionary<string, Network>;
		otherAliases!["test"] = testnet4;
		otherAliases["testnet"] = testnet4;
	}
}
