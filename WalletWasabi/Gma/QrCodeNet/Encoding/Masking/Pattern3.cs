using System;

namespace Gma.QrCodeNet.Encoding.Masking
{
	internal class Pattern3 : Pattern
	{
		public override bool this[int i, int j]
		{
			get => (j + i) % 3 == 0;
			set => throw new NotSupportedException();
		}

		public override MaskPatternType MaskPatternType => MaskPatternType.Type3;
	}
}
