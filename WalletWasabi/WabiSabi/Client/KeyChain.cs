using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.WabiSabi.Client;

public class KeyChain : BaseKeyChain
{
	public KeyChain(KeyManager keyManager, Kitchen kitchen) : base(kitchen)
	{
		if (keyManager.IsWatchOnly)
		{
			throw new ArgumentException("A watch-only keymanager cannot be used to initialize a keychain.");
		}
		KeyManager = keyManager;
	}

	private KeyManager KeyManager { get; }
	private ExtKey? MasterKey { get; set; }

	[MemberNotNull(nameof(MasterKey))]
	public void PreloadMasterKey()
	{
		MasterKey = KeyManager.GetMasterExtKey(Kitchen.SaltSoup());
	}

	protected override Key GetMasterKey()
	{
		if (MasterKey is null)
		{
			PreloadMasterKey();
		}
		return MasterKey.PrivateKey;
	}

	public override void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
	{
		foreach (var hdPubKey in KeyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			hdPubKey.SetKeyState(state);
		}
	}

	protected override BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
	{
		var hdKey = KeyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKey).Single();
		if (hdKey is null)
		{
			throw new InvalidOperationException($"The signing key for '{scriptPubKey}' was not found.");
		}
		if (hdKey.PrivateKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit) != scriptPubKey)
		{
			throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
		}
		var secret = hdKey.PrivateKey.GetBitcoinSecret(KeyManager.GetNetwork());
		return secret;
	}
}
