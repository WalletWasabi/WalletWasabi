using NBitcoin.Protocol;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

// This represents parameters all clients must agree on to produce a valid &
// standard transaction subject to constraints.
public record MultipartyTransactionParameters
{
	public static int SharedOverhead =  SharedOverheadFn(255, 255);

	// version, locktime, two 3 byte varints are non-witness data, marker and flags are witness data.
	private static int SharedOverheadFn(long minInputCount, long minOutputCount) =>
		4 * (4 + 4 + VarIntLength(minInputCount) + VarIntLength(minOutputCount)) + 1 + 1;

	private static int VarIntLength(long value) =>
		value switch
		{
			<  0xfd => 1,
			<= 0xffff => 3,
			<= 0xffffffff => 5,
			_ => 9
		};
}
