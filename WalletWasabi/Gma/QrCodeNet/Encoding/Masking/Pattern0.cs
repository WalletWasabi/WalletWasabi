namespace Gma.QrCodeNet.Encoding.Masking;

internal class Pattern0 : Pattern
{
	public override MaskPatternType MaskPatternType => MaskPatternType.Type0;

	public override bool this[int i, int j]
	{
		get => (j + i) % 2 == 0;
		set => throw new NotSupportedException();
	}
}
