namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

// This represents parameters all clients must agree on to produce a valid &
// standard transaction subject to constraints.
public record MultipartyTransactionParameters
{
	// version, locktime, two 3 byte varints are non-witness data, marker and flags are witness data.
	public static int SharedOverhead = 4 * (4 + 4 + 3 + 3) + 1 + 1;
}
