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

	/// <summary>
	/// The highest mining fee rate this signer will sign at, when it is a device that was authorized with a
	/// cap; null when no such cap exists. Rounds above it must be skipped before any input is registered,
	/// because the device refuses them at signing time and the coordinator then bans the registered inputs.
	/// </summary>
	FeeRate? MaxMiningFeeRate => null;

	OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData);

	Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin);
}
