using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Wallets;

namespace WalletWasabi.WabiSabi.Client;

public abstract class BaseKeyChain : IKeyChain
{
	public BaseKeyChain(Kitchen kitchen)
	{
		Kitchen = kitchen;
	}

	protected Kitchen Kitchen { get; }

	protected abstract Key GetMasterKey();

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

	public abstract void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts);

	protected abstract BitcoinSecret GetBitcoinSecret(Script scriptPubKey);
}
