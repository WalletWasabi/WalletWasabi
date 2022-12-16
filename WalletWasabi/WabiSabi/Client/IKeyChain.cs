using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;

namespace WalletWasabi.WabiSabi.Client;

public interface IKeyChain
{
	OwnershipProof GetOwnershipProof(SmartCoinAndSecret coinAndSecret, CoinJoinInputCommitmentData committedData)
	{
		return GetOwnershipProof(coinAndSecret.Coin.ScriptPubKey, coinAndSecret.Secret, committedData);
	}

	OwnershipProof GetOwnershipProof(Script scriptPubKey, BitcoinSecret bitcoinSecret, CoinJoinInputCommitmentData committedData);

	Transaction Sign(Transaction transaction, Coin coin, BitcoinSecret bitcoinSecret, PrecomputedTransactionData precomputeTransactionData);

	void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts);

	BitcoinSecret GetBitcoinSecret(Script scriptPubKey);
}
