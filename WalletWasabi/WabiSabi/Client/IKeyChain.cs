using WalletWasabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.WabiSabi.Client;

public interface IKeyChain
{
	/// <summary>
	/// Whether producing a signature takes a meaningful part of the signing phase, as it does when a device
	/// signs. The scheduler spreads signing requests over the phase to hide timing from the coordinator, which
	/// only holds when signing itself is instant; a signer that is not gets the phase instead of its leftovers.
	/// </summary>
	bool SigningTakesTime => false;

	OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData);

	Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin);
}
