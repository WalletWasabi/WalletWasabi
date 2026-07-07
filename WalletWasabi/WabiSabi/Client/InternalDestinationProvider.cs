using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Hwi.Trezor;

namespace WalletWasabi.WabiSabi.Client;

public class InternalDestinationProvider : IDestinationProvider
{
	public InternalDestinationProvider(KeyManager keyManager)
	{
		_keyManager = keyManager;

		// A Trezor coinjoin authorization is bound to the SLIP-25 taproot account, so all outputs must stay in it.
		SupportedScriptTypes = _keyManager.IsTrezorCoinJoinWallet()
			? [ScriptType.Taproot]
			: _keyManager.TaprootExtPubKey is not null
				? [ScriptType.P2WPKH, ScriptType.Taproot]
				: [ScriptType.P2WPKH];
	}

	private readonly KeyManager _keyManager;

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot)
	{
		// A Trezor coinjoin wallet can only sign outputs of the SLIP-25 taproot account, so it never uses segwit destinations.
		bool taprootOnly = _keyManager.IsTrezorCoinJoinWallet();

		// Get all locked internal keys we have and assert we have enough.
		_keyManager.AssertLockedInternalKeysIndexedAndPersist(count, preferTaproot || taprootOnly);

		var allKeys = _keyManager.GetNextCoinJoinKeys().ToList();
		var taprootKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86)
			.ToList();

		var segwitKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.Segwit)
			.ToList();

		var destinations = taprootOnly || (preferTaproot && taprootKeys.Count >= count)
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
