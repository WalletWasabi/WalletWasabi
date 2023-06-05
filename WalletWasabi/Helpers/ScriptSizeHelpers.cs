namespace WalletWasabi.Helpers;

public static class ScriptSizeHelpers
{
	public const int NonSegwitByteInWeightUnits = 4;
	public const int SegwitByteInWeightUnits = 1;
	public const int VirtualByteInWeightUnits = 4;

	public static int WeightUnitsToVirtualSize(int weightUnits) => weightUnits / VirtualByteInWeightUnits + (weightUnits % VirtualByteInWeightUnits == 0 ? 0 : 1); // ceiling(VirtualSize / VirtualByteInWeightUnits)

	public static int VirtualSize(int nonSegwitBytes, int segwitBytes) => WeightUnitsToVirtualSize(NonSegwitByteInWeightUnits * nonSegwitBytes + SegwitByteInWeightUnits * segwitBytes);
}
