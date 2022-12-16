using System.Collections.Generic;
using NBitcoin;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;

namespace WalletWasabi.WabiSabi.Client;

public interface IKeyChain
{
	OwnershipProof GetOwnershipProof(IDestination destination, BitcoinSecret bitcoinSecret, CoinJoinInputCommitmentData committedData);

	Transaction Sign(Transaction transaction, Coin coin, BitcoinSecret bitcoinSecret, PrecomputedTransactionData precomputeTransactionData);

	void TrySetScriptStates(KeyState state, IEnumerable<Script> scripts);
}
