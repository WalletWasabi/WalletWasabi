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

	public ImmutableArray<IDestination> GetNextDestinations(int count, bool preferTaproot)
	{
		// Get all locked internal keys we have and assert we have enough.
		_keyManager.AssertLockedInternalKeysIndexedAndPersist(count, preferTaproot);

		var allKeys = _keyManager.GetNextCoinJoinKeys();
		var taprootKeys = allKeys.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86);

		var destinations = preferTaproot && taprootKeys.Count() >= count
			? taprootKeys
			: allKeys.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.Segwit);

		return destinations.Select(x => (IDestination)x.GetAddress(_keyManager.GetNetwork())).ToImmutableArray();
	}

	public void TrySetScriptStates(KeyState state, ImmutableList<Script> scripts)
	{
		foreach (var hdPubKey in _keyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			_keyManager.SetKeyState(state, hdPubKey);
		}

		_keyManager.ToFile();
	}

	public ImmutableHashSet<ScriptType> SupportedScriptTypes { get; }
}
