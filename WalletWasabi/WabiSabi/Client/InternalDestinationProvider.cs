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

	public IEnumerable<IDestination> GetNextDestinations(int count)
	{
		// Get all locked internal keys we have and assert we have enough.
		KeyManager.AssertLockedInternalKeysIndexed(count);
		return KeyManager.GetKeys(x => x.IsInternal && x.KeyState == KeyState.Locked).Select(x => x.PubKey.WitHash);
	}
}
