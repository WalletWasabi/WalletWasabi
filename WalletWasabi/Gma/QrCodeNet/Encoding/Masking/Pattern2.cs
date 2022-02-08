namespace Gma.QrCodeNet.Encoding.Masking;

internal class Pattern2 : Pattern
{
	public override MaskPatternType MaskPatternType => MaskPatternType.Type2;

	public override bool this[int i, int j]
	{
		get => i % 3 == 0;
		set => throw new NotSupportedException();
	}
}
