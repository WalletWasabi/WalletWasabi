namespace WalletWasabi.WabiSabi.Client;

public interface IKeyChain
{
	/// <summary>True when signing needs device I/O; such a signer signs once up front, before the signatures go out on their schedule.</summary>
	bool SigningTakesTime => false;

	/// <summary>The fee cap the device was authorized with, or null. Rounds above it are skipped before any input is registered, since the device would refuse to sign and the coordinator would ban the inputs.</summary>
	FeeRate? MaxMiningFeeRate => null;

	/// <summary>True when the device no longer holds the authorization it signs with (its rounds are spent, or its session is gone), so the next start has to ask for one again.</summary>
	bool NeedsAuthorization => false;

	OwnershipProof GetOwnershipProof(IDestination destination, CoinJoinInputCommitmentData committedData);

	Transaction Sign(TransactionWithPrecomputedData unsignedCoinJoin, Coin coin);
}
