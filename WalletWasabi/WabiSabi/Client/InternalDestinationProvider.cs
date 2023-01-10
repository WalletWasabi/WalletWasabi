using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WabiSabi.Client;

public class InternalDestinationProvider : IDestinationProvider
{
	public InternalDestinationProvider(KeyManager keyManager)
	{
		KeyManager = keyManager;
	}

	private KeyManager KeyManager { get; }

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot)
	{
		// Get all locked internal keys we have and assert we have enough.
		KeyManager.AssertLockedInternalKeysIndexedAndPersist(count, preferTaproot);

		var allKeys = KeyManager.GetNextCoinJoinKeys().ToList();
		var taprootKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86)
			.ToList();
		
		var destinations = preferTaproot && taprootKeys.Count >= count
			? taprootKeys
			: allKeys;
		return destinations.Select(x => x.GetAddress(KeyManager.GetNetwork()));
	}
}
