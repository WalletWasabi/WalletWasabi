using NBitcoin;
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
			throw new ArgumentException("A watch-only keymanager cannot be used to initialize a keychain.");
		}
		KeyManager = keyManager;
		Kitchen = kitchen;
	}

	public KeyManager KeyManager { get; }
	private Kitchen Kitchen { get; }

	public OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData commitmentData)
	{
		var secret = GetBitcoinSecret(destination.ScriptPubKey);

		var masterKey = KeyManager.GetMasterExtKey(Kitchen.SaltSoup()).PrivateKey;
		var identificationMasterKey = Slip21Node.FromSeed(masterKey.ToBytes());
		var identificationKey = identificationMasterKey.DeriveChild("SLIP-0019").DeriveChild("Ownership identification key").Key;

		var signingKey = secret.PrivateKey;
		var ownershipProof = OwnershipProof.GenerateCoinJoinInputProof(
				signingKey,
				new OwnershipIdentifier(identificationKey, destination.ScriptPubKey),
				commitmentData);
		return ownershipProof;
	}

	public Transaction Sign(Transaction transaction, Coin coin, OwnershipProof ownershipProof)
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

		transaction.Sign(secret, coin);
		return transaction;
	}

	private BitcoinSecret GetBitcoinSecret(Script scriptPubKey)
	{
		var hdKey = KeyManager.GetSecrets(Kitchen.SaltSoup(), scriptPubKey).Single();
		if (hdKey is null)
		{
			throw new InvalidOperationException($"The signing key for '{scriptPubKey}' was not found.");
		}
		if (hdKey.PrivateKey.PubKey.WitHash.ScriptPubKey != scriptPubKey)
		{
			throw new InvalidOperationException("The key cannot generate the utxo scriptpubkey. This could happen if the wallet password is not the correct one.");
		}
		var secret = hdKey.PrivateKey.GetBitcoinSecret(KeyManager.GetNetwork());
		return secret;
	}
}
