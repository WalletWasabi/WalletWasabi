namespace WalletWasabi.WabiSabi.Client;

public interface IKeyChain
{
	/// <summary>True when signing needs device I/O; such a signer gets the whole signing phase instead of a random slot in it.</summary>
	bool SigningTakesTime => false;

	/// <summary>The fee cap the device was authorized with, or null. Rounds above it are skipped before any input is registered, since the device would refuse to sign and the coordinator would ban the inputs.</summary>
	FeeRate? MaxMiningFeeRate => null;

	OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData);

	Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin);
}
