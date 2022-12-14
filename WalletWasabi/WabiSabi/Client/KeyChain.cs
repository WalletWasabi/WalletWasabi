using System.Collections.Generic;
using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Wallets;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

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
	private Dictionary<Script, BitcoinSecret> BitcoinSecrets { get; } = new();

	[MemberNotNull(nameof(MasterKey))]
	public void PreloadMasterKey()
	{
		MasterKey = KeyManager.GetMasterExtKey(Kitchen.SaltSoup());
	}

	public void PreloadBitcoinSecrets(IEnumerable<Script> scriptPubKeys)
	{
		BitcoinSecrets.Clear();

		var hdKeyAndScripPubs = KeyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKeys.ToArray()).Zip(scriptPubKeys);

		foreach (var (hdKey, scriptPubKey) in hdKeyAndScripPubs)
		{
			BitcoinSecrets.Add(scriptPubKey, GetBitcoinSecret(scriptPubKey, hdKey));
		}
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
			KeyManager.SetKeyState(state, hdPubKey);
		}
	}

	protected override BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
	{
		if (BitcoinSecrets.TryGetValue(scriptPubKey, out var bitcoinSecret))
		{
			return bitcoinSecret;
		}

		var hdKey = KeyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKey).Single();

		return GetBitcoinSecret(scriptPubKey, hdKey);
	}

	private BitcoinSecret GetBitcoinSecret(Script scriptPubKey, ExtKey hdKey)
	{
		if (hdKey is null)
		{
			throw new InvalidOperationException($"The signing key for '{scriptPubKey}' was not found.");
		}

		var derivedScriptPubKeyType = scriptPubKey switch
		{
			_ when scriptPubKey.IsScriptType(ScriptType.P2WPKH) => ScriptPubKeyType.Segwit,
			_ when scriptPubKey.IsScriptType(ScriptType.Taproot) => ScriptPubKeyType.TaprootBIP86,
			_ => throw new NotSupportedException("Not supported script type.")
		};

		if (hdKey.PrivateKey.PubKey.GetScriptPubKey(derivedScriptPubKeyType) != scriptPubKey)
		{
			throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
		}
		var secret = hdKey.PrivateKey.GetBitcoinSecret(KeyManager.GetNetwork());
		return secret;
	}
}
