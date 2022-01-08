using NBitcoin;
using NBitcoin.Policy;
using System.Collections.Immutable;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

// This represents parameters all clients must agree on to produce a valid &
// standard transaction subject to constraints.
public record MultipartyTransactionParameters
{
	// version, locktime, two 3 byte varints are non-witness data, marker and flags are witness data.
	public static int SharedOverhead = 4 * (4 + 4 + 3 + 3) + 1 + 1;

	public static ImmutableSortedSet<ScriptType> OnlyP2WPKH = ImmutableSortedSet.Create<ScriptType>(ScriptType.P2WPKH);

	public MultipartyTransactionParameters(
		FeeRate feeRate,
		MoneyRange allowedInputAmounts,
		MoneyRange allowedOutputAmounts,
		Network network)
	{
		FeeRate = feeRate;
		AllowedInputAmounts = allowedInputAmounts;
		AllowedOutputAmounts = allowedOutputAmounts;
		Network = network;
	}

	// These parameters need to be committed to the transcript, but we want
	// the NBitcoin supplied default values, hence the private static property
	private static StandardTransactionPolicy StandardTransactionPolicy { get; } = new();

	public ImmutableSortedSet<ScriptType> AllowedInputTypes { get; init; } = OnlyP2WPKH;
	public ImmutableSortedSet<ScriptType> AllowedOutputTypes { get; init; } = OnlyP2WPKH;

	public int MaxTransactionSize { get; init; } = StandardTransactionPolicy.MaxTransactionSize!.Value;
	public FeeRate MinRelayTxFee { get; init; } = StandardTransactionPolicy.MinRelayTxFee;
	public FeeRate FeeRate { get; init; }
	public MoneyRange AllowedInputAmounts { get; init; }
	public MoneyRange AllowedOutputAmounts { get; init; }
	public Network Network { get; }

	// implied:
	// segwit transaction
	// version = 1
	// nLocktime = 0
	public Transaction CreateTransaction()
		=> Transaction.Create(Network);
}
