namespace WalletWasabi.WabiSabi.Client;

public class KeyChain : IKeyChain
{
	public KeyChain(KeyManager keyManager, string password)
	{
		if (keyManager.IsWatchOnly)
		{
			throw new ArgumentException("A watch-only key manager cannot be used to initialize a key chain.");
		}

		_keyManager = keyManager;
		_password = password;
	}

	private readonly KeyManager _keyManager;
	private readonly string _password;

	private Key GetMasterKey()
	{
		return _keyManager.GetMasterExtKey(_password).PrivateKey;
	}

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitmentData)
	{
		Key key = _keyManager.GetSecrets(_password, destination.ScriptPubKey).SingleOrDefault()
			?? throw new InvalidOperationException($"The signing key for '{destination.ScriptPubKey}' was not found.");
		Key masterKey = GetMasterKey();
		BitcoinSecret secret = key.GetBitcoinSecret(_keyManager.GetNetwork(), destination.ScriptPubKey);

		return NBitcoinExtensions.GetOwnershipProof(masterKey, secret, destination.ScriptPubKey, commitmentData);
	}

	public PSBT Sign(PSBT psbt)
	{
		if (psbt.Inputs.Count == 0)
		{
			throw new ArgumentException("No inputs to sign.", nameof(psbt));
		}

		var masterKey = _keyManager.GetMasterExtKey(_password);
		foreach (var input in psbt.Inputs)
		{
			if (input.GetTxOut() is { } utxo && _keyManager.TryGetKeyForScriptPubKey(utxo.ScriptPubKey, out var hdPubKey))
			{
				var key = masterKey.Derive(hdPubKey.FullKeyPath).PrivateKey;
				input.Sign(key.GetBitcoinSecret(_keyManager.GetNetwork(), utxo.ScriptPubKey).PrivateKey);
			}
		}

		return psbt;
	}
}
