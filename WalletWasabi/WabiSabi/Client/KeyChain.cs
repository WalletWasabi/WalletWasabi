using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class KeyChain : BaseKeyChain
{
	private readonly KeyManager _keyManager;

	public KeyChain(KeyManager keyManager, Kitchen kitchen):base(kitchen)
	{
		if (keyManager.IsWatchOnly)
		{
			throw new ArgumentException("A watch-only keymanager cannot be used to initialize a keychain.");
		}
		_keyManager = keyManager;
	}

	protected override Key GetMasterKey()
	{
		return _keyManager.GetMasterExtKey(Kitchen.SaltSoup()).PrivateKey;
	}

	public override void NotifyScriptState(IEnumerable<Script> scripts, KeyState state)
	{
		foreach (var hdPubKey in _keyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			hdPubKey.SetKeyState(state);
		}
	}

	protected override BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
	{
		{
			var hdKey = _keyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKey).Single();
			if (hdKey is null)
			{
				throw new InvalidOperationException($"The signing key for '{scriptPubKey}' was not found.");
			}
			if (hdKey.PrivateKey.PubKey.WitHash.ScriptPubKey != scriptPubKey)
			{
				throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
			}
			var secret = hdKey.PrivateKey.GetBitcoinSecret(_keyManager.GetNetwork());
			return secret;
		}
	}
}