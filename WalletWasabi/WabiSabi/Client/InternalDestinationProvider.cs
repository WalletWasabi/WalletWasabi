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

	// Keys stay locked until a coinjoin is broadcast, and several coinjoin clients can send their outputs to this
	// wallet at the same time (the wallet itself and the wallets that coinjoin into it). So that they do not all
	// get the same keys, a key that was handed out is held until its holder releases it.
	private readonly List<(HdPubKey Key, object Holder)> _heldKeys = [];
	private readonly Lock _lock = new();

	public IEnumerable<IDestination> GetNextDestinations(int count, bool preferTaproot, object holder)
	{
		lock (_lock)
		{
			var keys = _keyManager.GetNextCoinJoinKeys(count, preferTaproot, heldKeys: _heldKeys.Select(x => x.Key));
			_heldKeys.AddRange(keys.Select(key => (key, holder)));
			return keys.Select(x => x.GetAddress(_keyManager.GetNetwork()));
		}
	}

	public void ReleaseDestinations(object holder)
	{
		lock (_lock)
		{
			_heldKeys.RemoveAll(x => x.Holder == holder);
		}
	}

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
	{
		foreach (var hdPubKey in _keyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			_keyManager.SetKeyState(state, hdPubKey);
		}

		_keyManager.ToFile();
	}

	public ImmutableHashSet<ScriptType> SupportedScriptTypes { get; }
}
