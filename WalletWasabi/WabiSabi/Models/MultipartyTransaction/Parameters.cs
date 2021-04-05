using NBitcoin;
using NBitcoin.Policy;
using System.Collections.Immutable;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction
{
	// This represents parameters all clients must agree on to produce a valid &
	// standard transaction subject to constraints.
	public record Parameters(FeeRate FeeRate, MoneyRange AllowedInputAmounts, MoneyRange AllowedOutputAmounts, Network Network)
	{
		public static int SharedOverhead = 4*(4 + 4 + 3 + 3) + 1 + 1; // version, locktime, two 3 byte varints are non-witness data, marker and flags are witness data

		public static ImmutableSortedSet<ScriptType> OnlyP2WPKH = ImmutableSortedSet.Create<ScriptType>(ScriptType.P2WPKH);

		public ImmutableSortedSet<ScriptType> AllowedInputTypes { get; init; } = OnlyP2WPKH;
		public ImmutableSortedSet<ScriptType> AllowedOutputTypes { get; init; } = OnlyP2WPKH;

		// These parameters need to be committed to the transcript, but we want
		// the NBitcoin supplied default values, hence the private static property
		private static StandardTransactionPolicy StandardTransactionPolicy { get; } = new();
		public int MaxTransactionSize { get; init; } = (int)StandardTransactionPolicy.MaxTransactionSize;
		public FeeRate MinRelayTxFee { get; init; } = StandardTransactionPolicy.MinRelayTxFee;

		// implied:
		// segwit transaction
		// version = 1
		// nLocktime = 0
		public Transaction CreateTransaction()
			=> Transaction.Create(Network);
	}
}
