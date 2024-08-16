using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models.MultipartyTransaction;

// This represents parameters all clients must agree on to produce a valid &
// standard transaction subject to constraints.
public record MultipartyTransactionParameters
{
	public static readonly int SharedOverhead = SharedOverheadFn(255, 255);

	// version, locktime, two 3 byte varints are non-witness data, marker and flags are witness data.
	private static int SharedOverheadFn(long minInputCount, long minOutputCount)
	{
		var nonSegwitData =
			4 + // version
			4 + // locktime
			VarIntLength(minInputCount) +  // varint ins
			VarIntLength(minOutputCount);  // varint outs

		var segwitData =
			1 + // marker
			1;  // flag

		return VirtualSizeHelpers.VirtualSize(nonSegwitData, segwitData);
	}

	private static int VarIntLength(long value) =>
		value switch
		{
			<  0xfd => 1,
			<= 0xffff => 3,
			<= 0xffffffff => 5,
			_ => 9
		};
}
