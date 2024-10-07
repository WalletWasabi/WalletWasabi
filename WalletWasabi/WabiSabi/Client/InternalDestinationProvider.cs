using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;

namespace WalletWasabi.WabiSabi.Client;

public class InternalDestinationProvider : IDestinationProvider
{
	public InternalDestinationProvider(KeyManager keyManager)
	{
		_keyManager = keyManager;
	    SupportedScriptTypes = _keyManager.TaprootExtPubKey is not null
			? [ScriptType.P2WPKH, ScriptType.Taproot]
			: [ScriptType.P2WPKH];
	}

	private readonly KeyManager _keyManager;

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot)
	{
		// Get all locked internal keys we have and assert we have enough.
		_keyManager.AssertLockedInternalKeysIndexedAndPersist(count, preferTaproot);

		var allKeys = _keyManager.GetNextCoinJoinKeys().ToList();
		var taprootKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86)
			.ToList();

		var segwitKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.Segwit)
			.ToList();

		var destinations = preferTaproot && taprootKeys.Count >= count
			? taprootKeys
			: segwitKeys;
		return destinations.Select(x => x.GetAddress(_keyManager.GetNetwork()));
	}

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
	{
		foreach (var hdPubKey in _keyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			_keyManager.SetKeyState(state, hdPubKey);
		}

		_keyManager.ToFile();
	}

	public IEnumerable<ScriptType> SupportedScriptTypes { get; }
}
