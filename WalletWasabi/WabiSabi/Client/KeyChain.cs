using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public class KeyChain : IKeyChain
{
	public KeyChain(KeyManager keyManager, Kitchen kitchen)
	{
		if (keyManager.IsWatchOnly)
		{
			throw new ArgumentException("A watch-only key manager cannot be used to initialize a key chain.");
		}

		KeyManager = keyManager;
		Kitchen = kitchen;
	}

	private KeyManager KeyManager { get; }
	private Kitchen Kitchen { get; }

	private Key GetMasterKey()
	{
		return KeyManager.GetMasterExtKey(Kitchen.SaltSoup()).PrivateKey;
	}

	public void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts)
	{
		foreach (var hdPubKey in KeyManager.GetKeys(key => scripts.Any(key.ContainsScript)))
		{
			KeyManager.SetKeyState(state, hdPubKey);
		}
	}

	private BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
	{
		var hdKey = KeyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKey).Single();
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

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitmentData)
	{
		var secret = GetBitcoinSecret(destination.ScriptPubKey);

		var masterKey = GetMasterKey();
		var identificationMasterKey = Slip21Node.FromSeed(masterKey.ToBytes());
		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019")
			.DeriveChild("Ownership identification key").Key;

		var signingKey = secret.PrivateKey;
		var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
			signingKey,
			new OwnershipIdentifier(identificationKey, destination.ScriptPubKey),
			commitmentData,
			destination.ScriptPubKey.IsScriptType(ScriptType.P2WPKH)
				? ScriptPubKeyType.Segwit
				: ScriptPubKeyType.TaprootBIP86);
		return ownershipProof;
	}

	public Transaction Sign(Transaction transaction, Coin coin, PrecomputedTransactionData precomputedTransactionData)
	{
		transaction = transaction.Clone();
		if (transaction.Inputs.Count == 0)
		{
			throw new ArgumentException("No inputs to sign.", nameof(transaction));
		}

		var txInput = transaction.Inputs.AsIndexedInputs().FirstOrDefault(input => input.PrevOut == coin.Outpoint);

		if (txInput is null)
		{
			throw new InvalidOperationException("Missing input.");
		}

		var secret = GetBitcoinSecret(coin.ScriptPubKey);

		TransactionBuilder builder = Network.Main.CreateTransactionBuilder();
		builder.AddKeys(secret);
		builder.AddCoins(coin);
		builder.SetSigningOptions(new SigningOptions(TaprootSigHash.All, (TaprootReadyPrecomputedTransactionData)precomputedTransactionData));
		builder.SignTransactionInPlace(transaction);

		return transaction;
	}
}
