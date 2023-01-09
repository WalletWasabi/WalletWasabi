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
		var preferedScriptPubKeyType = preferTaproot
			? ScriptPubKeyType.TaprootBIP86
			: ScriptPubKeyType.Segwit;

		bool IsAvailable(HdPubKey hdPubKey) =>
			hdPubKey.IsInternal &&
			hdPubKey.KeyState == KeyState.Locked &&
			hdPubKey.FullKeyPath.GetScriptTypeFromKeyPath() == preferedScriptPubKeyType;

		return KeyManager
			.GetKeys(IsAvailable)
			.Select(x => x.GetAddress(KeyManager.GetNetwork()));
	}
}
